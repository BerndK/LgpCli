using System;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace Infrastructure
{
  public enum LogFileLocation
  {
    /// <summary>Use the path of the current system's temporary folder.</summary>
    TempDirectory,
    /// <summary>Use the path for a user's application data.</summary>
    LocalUserApplicationDirectory,
    /// <summary>Use the path for the application data that is shared among all users.</summary>
    CommonApplicationDirectory,
    /// <summary>Use the path for the executable file that started the application.</summary>
    ExecutableDirectory,
    /// <summary>Use the path for the executable file that started the application \Logs.</summary>
    ExecutableLogsDirectory,
    /// <summary>If the string specified by CustomLocation is not empty, then use it as the path. Otherwise, use the path for a user's application data.</summary>
    Custom,
  }

  public interface IAssemblyInfoProvider
  {
    public string Company { get; }
    public string Product { get; }
    public string Version { get; }
  }

  public abstract class AsmBasedAssemblyInfoProvider : IAssemblyInfoProvider
  {
    protected Lazy<Assembly>? Asm { get; set; }
    public string Company => Asm?.Value.GetCustomAttribute<AssemblyCompanyAttribute>()?.Company.Trim()
                             ?? throw new InvalidOperationException("Not able to get entry Assembly's Company Attribute");
    public string Product => Asm?.Value.GetCustomAttribute<AssemblyProductAttribute>()?.Product.Trim()
                             ?? throw new InvalidOperationException("Not able to get entry Assembly's Product Attribute");
    public string Version => Asm?.Value.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version.Trim()
                             ?? throw new InvalidOperationException("Not able to get entry Assembly's Version Attribute");
  }

  public class AssemblyInfoProvider : AsmBasedAssemblyInfoProvider
  {
    public AssemblyInfoProvider(Assembly asm)
    {
      this.Asm = new Lazy<Assembly>(() => asm);
    }
  }

  public class EntryAssemblyInfoProvider : AsmBasedAssemblyInfoProvider
  {
    public EntryAssemblyInfoProvider()
    {
      this.Asm = new Lazy<Assembly>(() =>
        Assembly.GetEntryAssembly() ?? throw new InvalidOperationException("Not able to get entry Assembly"));
    }
  }

  public class MockedAssemblyInfoProvider : IAssemblyInfoProvider
  {
    public MockedAssemblyInfoProvider(string company, string product, string version)
    {
      Company = company;
      Product = product;
      Version = version;
    }

    public string Company { get; }
    public string Product { get; }
    public string Version { get; }
  }

  public static class LocationPathHelper
  {
    public static string GetCompanyProductVersion(IAssemblyInfoProvider? assemblyInfoProvider)
    {
      if (assemblyInfoProvider == null) throw new ArgumentNullException(nameof(assemblyInfoProvider));
      return Path.Combine(assemblyInfoProvider.Company, assemblyInfoProvider.Product, assemblyInfoProvider.Version);
    }

    public static string GetTempAppDataPath(IAssemblyInfoProvider? assemblyInfoProvider) =>
      Path.Combine(Path.GetTempPath(), GetCompanyProductVersion(assemblyInfoProvider));

    public static string GetUserAppDataPath(IAssemblyInfoProvider? assemblyInfoProvider) =>
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), GetCompanyProductVersion(assemblyInfoProvider));

    public static string GetCommonAppDataPath(IAssemblyInfoProvider? assemblyInfoProvider) =>
      Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), GetCompanyProductVersion(assemblyInfoProvider));

    public static string GetStartUpPath() => System.AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);
  }

  public class SimpleFileRollingOptions
  {
    public SimpleFileRollingOptions()
    {
    }

    public int MaxSize { get; set; } = 1024 * 1024 * 10; //10MB
    public int? MaxFileCount { get; set; } = 10;

    public LogFileLocation Location { get; set; } = LogFileLocation.LocalUserApplicationDirectory;
    /// <summary>
    /// Path to log files
    /// </summary>
    public string? CustomLocation { get; set; }

    public string? LogFilePrefix { get; set; } = null;
    public string LogFileExt { get; set; } = ".log";
    public bool LogFilenameUseUtc { get; set; } = false;

    /// <summary>
    /// The function used to filter events based on the log level.
    /// </summary>
    public bool ReuseExistingFiles { get; set; } = true;
    public bool ThrowOnWriteError { get; set; } = false;
  }

  public class SimpleFileRolling : IDisposable
  {
    private readonly SimpleFileRollingOptions options;
    private readonly IAssemblyInfoProvider? assemblyInfoProvider;
    private StreamWriter? streamWriter = null;
    private DirectoryInfo? logFolder = null;
    private static object lockId = new object();
    private string InternalLogFilePrefix => this.options.LogFilePrefix ?? this.assemblyInfoProvider?.Product ?? "Log";
    private static SimpleFileRolling? instance;
    private bool writeErrorOccured = false;

    //multiple Logger instances shall share the same instance, to have a unique sink for logs in an application
    public static SimpleFileRolling Instance(SimpleFileRollingOptions options, IAssemblyInfoProvider? assemblyInfoProvider)
    {
      lock (lockId)
      {
        if (instance != null)
        {
          //check for same settings
          if (options != instance!.options)
            throw new InvalidOperationException($"Use the same {nameof(SimpleFileRollingOptions)} for all Loggers");
          if (assemblyInfoProvider != instance!.assemblyInfoProvider)
            throw new InvalidOperationException($"Use the same {nameof(IAssemblyInfoProvider)} for all Loggers");
        }
        else
        {
          instance = new SimpleFileRolling(options, assemblyInfoProvider);
        }
        return instance;
      }
    }

    private SimpleFileRolling(SimpleFileRollingOptions options, IAssemblyInfoProvider? assemblyInfoProvider)
    {
      this.options = options;
      this.assemblyInfoProvider = assemblyInfoProvider;
    }

    private FileInfo? LimitFiles()
    {
      if (!this.options.MaxFileCount.HasValue)
        return null;
      var existingFiles =
        GetFileNamesAndDateTime(LogFolder.FullName, this.InternalLogFilePrefix, this.options.LogFileExt)
          .OrderBy(e => e.timeStamp)
          .ToList();

      while (existingFiles.Count > 0 && existingFiles.Count > this.options.MaxFileCount.GetValueOrDefault())
      {
        existingFiles[0].fileInfo.Delete();
        existingFiles.RemoveAt(0);
      }

      return existingFiles.LastOrDefault().fileInfo;
    }

    private void OpenFile(string filepath)
    {
      streamWriter = File.AppendText(filepath);
      streamWriter.AutoFlush = true;
    }

    private DirectoryInfo LogFolder => this.logFolder ??= GetCheckLogFolder();
      
    private DirectoryInfo GetCheckLogFolder()
    {
      var result = new DirectoryInfo(BaseFolder());
      if (!result.Exists)
        result.Create();
      return result;
    }

    public static bool IsFolderWritable(string folderPath, bool throwIfFails = false)
    {
      try
      {
        using (FileStream fs = File.Create(Path.Combine(folderPath, Path.GetRandomFileName()),
                 1,
                 FileOptions.DeleteOnClose)
              )
        { }
        return true;
      }
      catch
      {
        if (throwIfFails)
          throw;
        else
          return false;
      }
    }

    private string CreateNewFilepath()
    {
      string? result;
      do
      {
        result = CreateNewFileNameFromDateTime(LogFolder.FullName, this.InternalLogFilePrefix, this.options.LogFileExt, this.options.LogFilenameUseUtc);
      } while (File.Exists(result));

      return result!;
    }

    #region DatedFilename

    private const string DateFormatForFilenameSec = "_yyyy-MM-dd_HH-mm-ss"; //_2013-03-15_16-54-30

    private static string CreateNewFileNameFromDateTime(string path, string? prefix, string ext, bool useUtc)
    {
      string filePath;
      do
      {
        var dateTime = useUtc ? DateTime.UtcNow : DateTime.Now;
        filePath = Path.Combine(path, prefix + dateTime.ToString(DateFormatForFilenameSec, CultureInfo.InvariantCulture));
        if (!string.IsNullOrEmpty(ext))
          filePath = Path.ChangeExtension(filePath, ext);
      } while (File.Exists(filePath));

      return filePath;
    }

    private static List<(FileInfo fileInfo, DateTime timeStamp)> GetFileNamesAndDateTime(string path, string prefix, string ext)
    {
      string formatStr = DateFormatForFilenameSec;
      formatStr = new string(formatStr
        .Select(c => (c == '_' || c == '-') ? c : '?')
        .ToArray());

      var di = new DirectoryInfo(path);

      if (!di.Exists)
        return new List<(FileInfo fileInfo, DateTime timeStamp)>();

      var filePattern = prefix + formatStr;
      if (!string.IsNullOrEmpty(ext))
        filePattern = Path.ChangeExtension(filePattern, ext);

      return di.GetFiles(filePattern)
        .Select(fi => (fi, GetDateTimeFromFileName(fi.Name, prefix)))
        .ToList();
    }

    private static DateTime GetDateTimeFromFileName(string? fileName, string prefix)
    {
      if (fileName == null)
        return default(DateTime);
      var lenOfPrefix = (prefix ?? string.Empty).Length;
      var datepart = Path.GetFileNameWithoutExtension(fileName).Remove(0, lenOfPrefix);
      DateTime result;
      if (!DateTime.TryParseExact(datepart, DateFormatForFilenameSec, null, DateTimeStyles.None, out result))
        result = DateTime.MinValue;
      return result;
    }
    #endregion

    internal void WriteLine(string message)
    {
      if (writeErrorOccured && !options.ThrowOnWriteError)
        return;
      try
      {
        lock (this)
        {
          if (streamWriter == null)
          {
            var latestFileInfo = LimitFiles();
            var newFilepath = this.options.ReuseExistingFiles && latestFileInfo != null && latestFileInfo.Exists && latestFileInfo.Length < this.options.MaxSize
              ? latestFileInfo.FullName
              : CreateNewFilepath();
            OpenFile(newFilepath);
          }

          streamWriter!.WriteLine(message);
          if (streamWriter.BaseStream.Position > this.options.MaxSize)
          {
            streamWriter.Close();
            streamWriter = null;
          }
        }
      }
      catch
      {
        writeErrorOccured = true;
        if (options.ThrowOnWriteError)
          throw;
      }
    }

    internal string BaseFolder() =>
      BaseFolder(this.options.Location, this.assemblyInfoProvider, this.options.CustomLocation);

    public static string BaseFolder(LogFileLocation logFileLocation, IAssemblyInfoProvider? assemblyInfoProvider, string? customLocation)
    {
      switch (logFileLocation)
      {
        case LogFileLocation.TempDirectory:
          return LocationPathHelper.GetTempAppDataPath(assemblyInfoProvider);
        case LogFileLocation.LocalUserApplicationDirectory:
          return LocationPathHelper.GetUserAppDataPath(assemblyInfoProvider); //this is not LocalUserAppDataPath?!
        case LogFileLocation.CommonApplicationDirectory:
          return LocationPathHelper.GetCommonAppDataPath(assemblyInfoProvider);
        case LogFileLocation.ExecutableDirectory:
          return LocationPathHelper.GetStartUpPath();
        case LogFileLocation.ExecutableLogsDirectory:
          return Path.Combine(LocationPathHelper.GetStartUpPath(), "Logs");
        case LogFileLocation.Custom:
          return string.IsNullOrEmpty(customLocation) ? LocationPathHelper.GetUserAppDataPath(assemblyInfoProvider) : customLocation!;
        default:
          return LocationPathHelper.GetUserAppDataPath(assemblyInfoProvider);
      }
    }

    public void Dispose()
    {
      streamWriter?.Flush();
      streamWriter?.Dispose();
    }
  }
}