﻿namespace DalaMock.Host.Mediator;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Plugin.Services;
using Microsoft.Extensions.Hosting;

public class MediatorService : IHostedService
{
    private readonly object addRemoveLock = new();
    private readonly Dictionary<object, DateTime> lastErrorTime = new();
    private readonly CancellationTokenSource loopCts = new();
    private readonly ConcurrentQueue<MessageBase> messageQueue = new();
    private readonly Dictionary<Type, HashSet<SubscriberAction>> subscriberDict = new();

    public MediatorService(IPluginLog logger)
    {
        this.Logger = logger;
    }

    public IPluginLog Logger { get; }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        this.Logger.Verbose("Starting service {type} ({this})", this.GetType().Name, this);

        _ = Task.Run(
            async () =>
            {
                while (!this.loopCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(100, this.loopCts.Token).ConfigureAwait(false);

                    HashSet<MessageBase> processedMessages = new();
                    while (this.messageQueue.TryDequeue(out var message))
                    {
                        if (processedMessages.Contains(message))
                        {
                            continue;
                        }

                        processedMessages.Add(message);

                        this.ExecuteMessage(message);
                    }
                }
            });
        this.Logger.Verbose("Started service {type} ({this})", this.GetType().Name, this);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        this.Logger.Verbose("Stopping service {type} ({this})", this.GetType().Name, this);

        this.messageQueue.Clear();
        this.loopCts.Cancel();
        return Task.CompletedTask;
    }

    public void PrintSubscriberInfo()
    {
        foreach (var kvp in this.subscriberDict.SelectMany(c => c.Value.Select(v => v))
                     .DistinctBy(p => p.Subscriber).OrderBy(
                         p => p.Subscriber.GetType().FullName,
                         StringComparer.Ordinal).ToList())
        {
            var type = kvp.Subscriber.GetType().Name;
            var sub = kvp.Subscriber.ToString();
            this.Logger.Information($"Subscriber {type}: {sub}");
            StringBuilder sb = new();
            sb.Append("=> ");
            foreach (var item in this.subscriberDict.Where(item => item.Value.Any(v => v.Subscriber == kvp.Subscriber))
                         .ToList())
            {
                sb.Append(item.Key.Name).Append(", ");
            }

            if (!string.Equals(sb.ToString(), "=> ", StringComparison.Ordinal))
            {
                this.Logger.Information("{sb}", sb.ToString());
            }

            this.Logger.Information("---");
        }
    }

    public void Publish<T>(T message) where T : MessageBase
    {
        if (message.KeepThreadContext)
        {
            this.ExecuteMessage(message);
        }
        else
        {
            this.messageQueue.Enqueue(message);
        }
    }

    public void Publish(List<MessageBase>? messages)
    {
        if (messages != null)
        {
            foreach (var message in messages)
            {
                if (message.KeepThreadContext)
                {
                    this.ExecuteMessage(message);
                }
                else
                {
                    this.messageQueue.Enqueue(message);
                }
            }
        }
    }

    public void Subscribe<T>(IMediatorSubscriber subscriber, Action<T> action) where T : MessageBase
    {
        lock (this.addRemoveLock)
        {
            this.subscriberDict.TryAdd(typeof(T), new HashSet<SubscriberAction>());

            if (!this.subscriberDict[typeof(T)].Add(new SubscriberAction(subscriber, action)))
            {
                throw new InvalidOperationException("Already subscribed");
            }

            this.Logger.Verbose(
                "Subscriber added for message {message}: {sub}",
                typeof(T).Name,
                subscriber.GetType().Name);
        }
    }

    public void Unsubscribe<T>(IMediatorSubscriber subscriber) where T : MessageBase
    {
        lock (this.addRemoveLock)
        {
            if (this.subscriberDict.ContainsKey(typeof(T)))
            {
                this.subscriberDict[typeof(T)].RemoveWhere(p => p.Subscriber == subscriber);
            }
        }
    }

    public void UnsubscribeAll(IMediatorSubscriber subscriber)
    {
        lock (this.addRemoveLock)
        {
            foreach (var kvp in this.subscriberDict.Select(k => k.Key))
            {
                var unSubbed = this.subscriberDict[kvp]?.RemoveWhere(p => p.Subscriber == subscriber) ?? 0;
                if (unSubbed > 0)
                {
                    this.Logger.Verbose("{sub} unsubscribed from {msg}", subscriber.GetType().Name, kvp.Name);
                }
            }
        }
    }

    private void ExecuteMessage(MessageBase message)
    {
        if (!this.subscriberDict.TryGetValue(message.GetType(), out var subscribers) || subscribers == null ||
            !subscribers.Any())
        {
            return;
        }

        HashSet<SubscriberAction> subscribersCopy;
        lock (this.addRemoveLock)
        {
            subscribersCopy = subscribers?.Where(s => s.Subscriber != null).ToHashSet() ??
                              new HashSet<SubscriberAction>();
        }

        foreach (var subscriber in subscribersCopy)
        {
            try
            {
                typeof(MediatorService)
                    .GetMethod(nameof(this.ExecuteSubscriber), BindingFlags.NonPublic | BindingFlags.Instance)?
                    .MakeGenericMethod(message.GetType())
                    .Invoke(this, new object[] { subscriber, message });
            }
            catch (Exception ex)
            {
                if (this.lastErrorTime.TryGetValue(subscriber, out var lastErrorTime) &&
                    lastErrorTime.Add(TimeSpan.FromSeconds(10)) > DateTime.UtcNow)
                {
                    continue;
                }

                this.Logger.Error(
                    ex,
                    "Error executing {type} for subscriber {subscriber}",
                    message.GetType().Name,
                    subscriber.Subscriber.GetType().Name);
                this.lastErrorTime[subscriber] = DateTime.UtcNow;
            }
        }
    }

    private void ExecuteSubscriber<T>(SubscriberAction subscriber, T message)
        where T : MessageBase
    {
        var isSameThread = message.KeepThreadContext ? "$" : string.Empty;
        ((Action<T>)subscriber.Action).Invoke(message);
    }

    private sealed class SubscriberAction
    {
        public SubscriberAction(IMediatorSubscriber subscriber, object action)
        {
            this.Subscriber = subscriber;
            this.Action = action;
        }

        public object Action { get; }

        public IMediatorSubscriber Subscriber { get; }
    }
}