using Pek.Logging;

XXTrace.UseConsole();

//var setting = XXTraceSetting.Current;
//var baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
//var configFile = Path.Combine(baseDirectory, "Config", "Core.config");
//var logDirectory = Path.Combine(baseDirectory, setting.LogPath);

//Console.WriteLine("XXTrace Sample");
//Console.WriteLine($"BaseDirectory: {baseDirectory}");
//Console.WriteLine($"ConfigFile: {configFile}");
//Console.WriteLine($"BeforeExists: {File.Exists(configFile)}");

//setting.Normalize();
//setting.Save();

//Console.WriteLine($"AfterExists: {File.Exists(configFile)}");
//Console.WriteLine("Current Setting Snapshot:");
//Console.WriteLine($"  Debug = {setting.Debug}");
//Console.WriteLine($"  LogLevel = {setting.LogLevel}");
//Console.WriteLine($"  LogPath = {setting.LogPath}");
//Console.WriteLine($"  DataPath = {setting.DataPath}");

//XXTrace.UseConsole();
//XXTrace.WriteLine("XXTrace sample start");
//XXTrace.WriteLine("Current log level: {0}", setting.LogLevel);
//XXTrace.WriteException(new InvalidOperationException("XXTrace sample exception"));

//await Task.Delay(500);
//XXTrace.Shutdown();

//Console.WriteLine();
//Console.WriteLine($"LogDirectoryExists: {Directory.Exists(logDirectory)}");

//if (Directory.Exists(logDirectory))
//{
//    var logFiles = Directory.GetFiles(logDirectory, "*.log", SearchOption.TopDirectoryOnly)
//        .OrderByDescending(e => e)
//        .ToArray();

//    Console.WriteLine($"LogFileCount: {logFiles.Length}");
//    if (logFiles.Length > 0)
//    {
//        Console.WriteLine($"LatestLogFile: {logFiles[0]}");
//        Console.WriteLine("LatestLogContent:");
//        Console.WriteLine(File.ReadAllText(logFiles[0]));
//    }
//}

//if (File.Exists(configFile))
//{
//    Console.WriteLine();
//    Console.WriteLine("Core.config Content:");
//    Console.WriteLine(File.ReadAllText(configFile));
//}

//Console.WriteLine();
//Console.WriteLine("Done.");

XXTrace.WriteLine($"开始测试");

Console.ReadKey();