using CriticalCommonLib;
using Dalamud;
using Dalamud.Plugin.Services;
using Lumina;
using ILogger = Serilog.ILogger;

namespace DalaMock.Dalamud;

public class MockService
{
    private MockContainer _mockContainer;
    private readonly MockProgram _mockProgram;
    private readonly MockPluginInterfaceService _mockPluginInterfaceService;
    private readonly GameData _gameData;
    private readonly ClientLanguage _clientLanguage;
    private readonly ILogger _log;
    private readonly IPluginLog _pluginLog;
    
    private MockPluginLog _mockPluginLog;
    private MockClientState _mockClientState;
    private MockDataManager _mockDataManager;
    private MockFramework _mockFramework;
    private MockKeyState _mockKeyState;
    private MockCommandManager _mockCommandManager;
    private MockTextureManager _mockTextureManger;
    private MockTextureProvider _textureProvider;
    private MockGameGui _mockGameGui;

    public MockPluginLog MockPluginLog => _mockPluginLog;

    public MockClientState MockClientState => _mockClientState;

    public MockDataManager MockDataManager => _mockDataManager;

    public MockFramework MockFramework => _mockFramework;

    public MockKeyState MockKeyState => _mockKeyState;

    public MockCommandManager MockCommandManager => _mockCommandManager;

    public MockTextureManager MockTextureManger => _mockTextureManger;

    public MockTextureProvider TextureProvider => _textureProvider;
    
    public MockGameGui MockGameGui => _mockGameGui;

    public MockService(MockProgram mockProgram, GameData gameData, ClientLanguage clientLanguage, ILogger log)
    {
        _mockProgram = mockProgram;
        _gameData = gameData;
        _clientLanguage = clientLanguage;
        _log = log;
        CreateMockServices();
    }

    private void CreateMockServices()
    {
        _mockPluginLog = new MockPluginLog(_log);
        _mockClientState = new MockClientState();
        _mockDataManager = new MockDataManager(_gameData, _clientLanguage);
        _mockFramework = new MockFramework();
        _mockKeyState = new MockKeyState(_mockProgram.Window);
        _mockCommandManager = new MockCommandManager(_mockPluginLog,_clientLanguage);
        _mockTextureManger = new MockTextureManager(_mockProgram.GraphicsDevice,_mockProgram.Controller, _mockFramework, _mockDataManager, _clientLanguage );
        _textureProvider = new MockTextureProvider(_mockTextureManger);
        _mockGameGui = new MockGameGui();

        _mockContainer = new MockContainer(_mockPluginLog);
        _mockContainer.AddInstance(typeof(IClientState), _mockClientState);
        _mockContainer.AddInstance(typeof(IDataManager), _mockDataManager);
        _mockContainer.AddInstance(typeof(IFramework), _mockFramework);
        _mockContainer.AddInstance(typeof(IKeyState), _mockKeyState);
        _mockContainer.AddInstance(typeof(ICommandManager), _mockCommandManager);
        _mockContainer.AddInstance(typeof(ITextureProvider), _textureProvider);
        _mockContainer.AddInstance(typeof(IGameGui), _mockGameGui);
        _mockContainer.Create<Service>();
        
        MockContainer mockContainer = new MockContainer(_mockPluginLog);
        mockContainer.AddInstance(typeof(IClientState), _mockClientState);
        mockContainer.AddInstance(typeof(IDataManager), _mockDataManager);
        mockContainer.AddInstance(typeof(IFramework), _mockFramework);
        mockContainer.AddInstance(typeof(IKeyState), _mockKeyState);
        mockContainer.AddInstance(typeof(ICommandManager), _mockCommandManager);
        mockContainer.AddInstance(typeof(ITextureProvider), _textureProvider);
        mockContainer.AddInstance(typeof(IGameGui), _mockGameGui);
        mockContainer.Create<Service>();        
    }
}