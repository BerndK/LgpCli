using System;
using System.Buffers;
using System.Reflection;
using System.Text;

namespace Infrastructure
{
  public static class ToolBox
  {
    public static string GetStartupPath()
    {
      //alternative to System.Reflection.Assembly.GetEntryAssembly().Location :
      //                           Process.GetCurrentProcess().MainModule.FileName
      //does not work https://stackoverflow.com/q/58428375/1797939

      return System.AppContext.BaseDirectory?.TrimEnd(Path.DirectorySeparatorChar)
             //old implementation:
             //return Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location)
             //#if NET8_0_OR_GREATER
             //  ?? Path.GetDirectoryName(Environment.ProcessPath)
             //#endif
             ?? throw new InvalidOperationException();
    }
    public static DisposableValue<string> CreateTempFolder()
    {
      var tempFolder = Path.GetTempFileName();
      File.Delete(tempFolder);
      var di = new DirectoryInfo(tempFolder);
      di.Create();
      return Disposable.Create(() => di.Delete(true), tempFolder);
    }

    public static string CreateTempFolderNonDisposable()
    {
      var tempFolder = Path.GetTempFileName();
      File.Delete(tempFolder);
      var di = new DirectoryInfo(tempFolder);
      di.Create();
      return tempFolder;
    }

    public static DisposableValue<string> CreateTempFile()
    {
      var tempFile = Path.GetTempFileName();
      return Disposable.Create(() => File.Delete(tempFile), tempFile);
    }

    public static DisposableValue<FileInfo> CreateTempFileInfo()
    {
      var tempFileInfo = new FileInfo(Path.GetTempFileName());
      return Disposable.Create(() => tempFileInfo.Delete(), tempFileInfo);
    }

    public static MemoryStream StringToUtf8MemoryStream(string text)
    {
      var ms = new MemoryStream();
      using (var sw = new StreamWriter(ms, new UTF8Encoding(false, true), 1024, true))
      {
        sw.Write(text);
      }

      ms.Seek(0, SeekOrigin.Begin);
      return ms;
    }

    public static IEnumerable<T> SearchItems<T>(this IEnumerable<T> sequence, string searchText,
      Func<T, string> stringSelector, string? tokenSeparator, StringComparison comparisonType)
    {
      ReadOnlySpan<string> searchTokens = tokenSeparator != null
        ? new ReadOnlySpan<string>(searchText.Split(tokenSeparator, StringSplitOptions.RemoveEmptyEntries))
        : new ReadOnlySpan<string>(new string[] { searchText });
      
      var searchValues = SearchValues.Create(searchTokens, comparisonType);
      return sequence.Where(item => stringSelector.Invoke(item).AsSpan().IndexOfAny(searchValues) >= 0);

    }

    public static IEnumerable<T> SearchItemsOldStyle<T>(this IEnumerable<T> sequence, string searchText,
      Func<T, string> stringSelector, string? tokenSeparator, StringComparison comparisonType)
    {
      var searchTokens = tokenSeparator != null
        ? searchText.Split(tokenSeparator, StringSplitOptions.RemoveEmptyEntries)
        : new string[] { searchText };

      return sequence
        .Where(item =>
        {
          var s = stringSelector.Invoke(item);
          return searchTokens.Any(token => s.IndexOf(token, comparisonType) >= 0);
        });
    }

    public static string? GetInfos()
    {
      return Assembly.GetEntryAssembly()?.FullName;
    }

    public static string TrimPrefix(string s, string prefix)
    {
      if (s.StartsWith(prefix))
        return s[prefix.Length..];
      return s;
    }

    public static List<string> SplitEscaped(string escaped, char separatorChar, int maxCount = Int32.MaxValue)
    {
      string? currentResult = null;
      var result = new List<string>();
      var s = escaped.AsSpan();
      while (!s.IsEmpty)
      {
        if (result.Count >= maxCount - 1)
        {
          result.Add(currentResult + s.ToString());
          return result;
        }
        var idx = s.IndexOf(separatorChar);
        if (idx >= 0)
        {
          //detect double separator -> escaped separator
          var doubleSep = s.Length > (idx + 1) && s[idx] == s[idx + 1];
          if (doubleSep)
            idx++; //skip one of the doubles
          currentResult += s.Slice(0, idx).ToString();
          s = s.Slice(idx + 1);
          if (!doubleSep)
          {
            result.Add(currentResult);
            currentResult = null;
          }
        }
        else
        {
          result.Add(currentResult + s.ToString());
          return result;
        }
      }
      result.Add(currentResult + s.ToString());
      return result;
    }

    public static string EscapeSeparator(string plain, char separatorChar)
    {
      return plain.Replace(separatorChar.ToString(), new string(separatorChar, 2));
    }

  }
}
