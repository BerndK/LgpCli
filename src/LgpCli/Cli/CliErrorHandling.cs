using System.ComponentModel;
using System.Runtime.InteropServices;
using Infrastructure;
using Microsoft.Extensions.Logging;

namespace Cli;

static internal class CliErrorHandling
{
  public static void HandleShowExceptions(Action action, ILogger? logger)
  {
    try
    {
      action();
    }
    catch (Exception e)
    {
      ShowErrorMessage(e, out var showCallStack, logger);
      if (showCallStack)
      {
        Console.WriteLine(e);
      }
      CliTools.EnterToContinue();
    }
  }

  private static void ShowErrorMessage(Exception e, out bool showCallStack, ILogger? logger)
  {
    showCallStack = true;
    if (e is AggregateException ae)
    {
      logger?.LogError("AggregateException follows...");
      foreach (var ex in ae.InnerExceptions) ShowErrorMessage(ex, out showCallStack, logger);
      return;
    }
    else
    {
      if (e is AbortException)
      {
        var sError = "\r\nOperation aborted";
        if (!string.IsNullOrWhiteSpace(e.Message))
          sError += $":{e.Message}";
        
        logger?.LogWarning($"Operation aborted:{e.Message}");
        CliTools.WriteLine(CliTools.ErrorColor, sError);
        showCallStack = false;
        return;
      }

      if (e is OperationCanceledException)
      {
        logger?.LogWarning(e, $"Operation cancelled:{e.Message}");
        CliTools.WriteLine(CliTools.ErrorColor, $"\r\nOperation cancelled:{e.Message}");
        return;
      }

      if (e is Win32Exception we)
      {
        var errorCode = we.ErrorCode; //HResult (COM-Error, inherited from ExternalException, most cases 0x80004005 -2147467259 Unknown Error (HRESULT.E_FAIL)  
        var nativeErrorCode = we.NativeErrorCode;
        var sErrorCode = errorCode != 0 && errorCode != -2147467259
          ? $"({errorCode})"
          : null;
        var sNativeError = nativeErrorCode != 0 && nativeErrorCode != -2147467259
#if NET6_0_OR_GREATER
          ? Marshal.GetPInvokeErrorMessage(nativeErrorCode)
#else
          ? Marshal.GetLastWin32Error().ToString()
#endif
          : null;
        
        var sNativeErrorCode = nativeErrorCode != 0 && nativeErrorCode != -2147467259
          ? $"[{we.NativeErrorCode}]"
          : null;

        var sError = e.Message;

        //add errorMessage retrieved from Win32 if not already the same text
        if (sNativeError != sError)
        {
          if (!string.IsNullOrWhiteSpace(sError))
            sError += "; ";
          sError += sNativeError;
        }

        if (sNativeError != null)
          sError += " " + sNativeErrorCode;

        if (sErrorCode != null)
          sError += " " + sErrorCode;

        logger?.LogError(e, sError);
        CliTools.WriteLine(CliTools.ErrorColor, $"\r\nERROR: {sError}");

        return;
      }

      logger?.LogError(e, e.Message);
      CliTools.WriteLine(CliTools.ErrorColor, $"\r\nERROR: {e.Message}");
    }
  }
}