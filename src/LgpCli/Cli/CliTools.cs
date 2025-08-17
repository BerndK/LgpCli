using System;
using System.Collections.Generic;
using System.Data.SqlTypes;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Infrastructure;

namespace Cli
{
  public class MenuItem
  {
    /// <summary>
    /// Initializes a new instance of the <see cref="MenuItem"/> class.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="text">The text.</param>
    /// <param name="action">The action.</param>
    /// <param name="canExecuteFunc">func to return true or false to define if can be executed, null to be invisible</param>
    /// <remarks>Set <see cref="AdminRequired"/> to request Admin Privileges</remarks>
    public MenuItem(string? key, string text, Action? action, Func<bool?>? canExecuteFunc = null) : this(text, action, canExecuteFunc)
    {
      this.Key = key;
    }

    public MenuItem(string text)
    {
      this.Text = text;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuItem"/> class.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="action">The action.</param>
    /// <param name="canExecuteFunc">func to return true or false to define if can be executed, null to be invisible</param>
    /// <remarks>Set <see cref="AdminRequired"/> to request Admin Privileges</remarks>
    public MenuItem(string text, Action? action, Func<bool?>? canExecuteFunc = null) : this(text)
    {
      this.Action = action;
      this.CanExecuteFunc = canExecuteFunc;
    }

    public string? Key { get; set; }
    public string Text { get; set; }
    public Action? Action { get; set; }
    public object? Tag { get; set; }
    public Func<bool?>? CanExecuteFunc { get; set; }
    private bool? canExecuteCached;
    public bool CanExecute
    {
      get
      {
        if (this.CanExecuteFunc == null)
        {
          canExecuteCached = true;
        }
        else
        {
          this.canExecuteCached = this.canExecuteCached.HasValue ? this.canExecuteCached : this.CanExecuteFunc();
        }
        return canExecuteCached.GetValueOrDefault(false);
      }
    }

    public bool Visible
    {
      get
      {
        if (this.CanExecuteFunc == null)
        {
          canExecuteCached = true;
        }
        else
        {
          this.canExecuteCached = this.canExecuteCached.HasValue ? this.canExecuteCached : this.CanExecuteFunc();
        }
        return canExecuteCached != null;
      }
    }

    public bool AdminRequired { get; set; }
    public bool HiddenButActive { get; set; }
  }

  public class MenuSeparator : MenuItem
  {
    public MenuSeparator(string text) : base(text) { }
    
  }

  public static partial class CliTools
  {
    public static ConsoleColor TitleColor => ConsoleColor.White;
    public static ConsoleColor HighlightColor => ConsoleColor.White;
    public static ConsoleColor MenuSelectColor => ConsoleColor.Yellow;
    public static ConsoleColor MenuInactiveSelectColor => ConsoleColor.DarkYellow;
    public static ConsoleColor MenuColor => ConsoleColor.Gray;
    public static ConsoleColor MenuInactiveColor => ConsoleColor.DarkGray;
    public static ConsoleColor ErrorColor => ConsoleColor.Red;
    public static ConsoleColor WarnColor => ConsoleColor.DarkYellow;

    public static ConsoleColor OkColor => ConsoleColor.Green;
    public static ConsoleColor NotOkColor => ConsoleColor.Red;


    public static MenuItem ShowMenu(string? selectOptionText, params MenuItem[] items)
    {
      return ShowMenu(selectOptionText, -1, items);
    }

    public class MenuSettings
    {
      public static MenuSettings Default => new MenuSettings();
      public bool InitialBlankLine { get; set; } = true;
      public bool SelectBlankLine { get; set; } = true;
      public string Indent { get; set; } = string.Empty;
      public int StartIndex { get; set; } = 1;
      public int MaxItemCount { get; set; } = 1001; //1000 items + the escape item

      public MenuSettings WithMaxItemCount(int maxItemCount)
      {
        MaxItemCount = maxItemCount;
        return this;
      }
      public bool SearchInText { get; set; } = false;
    }

    public static MenuItem ShowMenu(string? selectOptionText, int defaultIndex, params MenuItem[] items)
    {
      return ShowMenu(selectOptionText, defaultIndex, new MenuSettings(), items);
    }

    public static MenuItem ShowMenu(string? selectOptionText, int defaultIndex, MenuSettings settings, params MenuItem[] items)
    {
      if (settings.InitialBlankLine)
        Console.WriteLine();
      var keys = new List<ConsoleKey>();
      var current = settings.StartIndex;
      var countVisibleItems = 0;
      foreach (var item in items.Take(settings.MaxItemCount))
      {
        if (item is MenuSeparator)
        {
          if (item.Visible)
          {
            CliTools.WriteLine(MenuInactiveColor, $"{settings.Indent}      {item.Text}");
          }
        }
        else
        {
          if (item.Visible)
          {
            if (string.IsNullOrEmpty(item.Key))
            {
              item.Key = current.ToString();
              current++;
            }

            if (!item.HiddenButActive)
            {
              var canExecute = item.CanExecute;
              Write(canExecute ? MenuSelectColor : MenuInactiveSelectColor, $"{settings.Indent}{item.Key,3} : ");
              using (canExecute ? Disposable.Empty : MenuInactiveColor.Switch())
              {
                MarkupLine(item.Text);
              }
            }
            countVisibleItems++;
          }
        }
      }
      if (items.Length > settings.MaxItemCount)
        CliTools.WriteLine(WarnColor, $"Incomplete List shown here ({countVisibleItems-1} of {items.Length - 1}), enter part of the text to filter items!");

      var escItem = items.FirstOrDefault(i => string.Equals(i.Key, "ESC", StringComparison.OrdinalIgnoreCase));

      var defaultValue = defaultIndex >= 0 && defaultIndex < items.Length
        ? items[defaultIndex].Key
        : null;
      do
      {
        if (selectOptionText == null)
        {
          selectOptionText = "Select your option: ";
          if (settings.SelectBlankLine)
            selectOptionText = "\r\n" + selectOptionText;
        }
        
        if (!selectOptionText.StartsWith("\r\n") && settings.SelectBlankLine)
          selectOptionText = "\r\n" + selectOptionText;
        if (!selectOptionText.EndsWith(": "))
          selectOptionText += ": ";
       
        Console.Write($"{settings.Indent}{selectOptionText}");

        string input;
        using (MenuSelectColor.Switch())
        {
          ReadLineEx(out input, defaultValue, escapeText:escItem?.Key);
        }
        var selectedItem = items
          .FirstOrDefault(i => string.Equals(i.Key, input, StringComparison.OrdinalIgnoreCase) && i.CanExecute);
        if (selectedItem != null)
        {
          if (selectedItem.AdminRequired)
            Uac.RequestAdminRoleRestart(ConfirmAdminRestart);
          selectedItem.Action?.Invoke();
          return selectedItem;
        }
        else
        {
          defaultValue = null; //no more default when a wrong item has been entered

          //show a new Menu with a subset, if not all items are shown
          //use the entered text to limit the list
          if (items.Length > settings.MaxItemCount || settings.SearchInText) 
          {
            var filteredItems = items
#if NETCOREAPP2_1_OR_GREATER
              .Where(e => e.Text.Contains(input, StringComparison.OrdinalIgnoreCase))
#else
              .Where(e => e.Text.ToLower().Contains(input.ToLower()))
#endif
              .ToList();
            if (filteredItems.Any())
            { //sub-select for matching items
              if (escItem != null && !filteredItems.Contains(escItem))
                filteredItems.Add(escItem);
              return ShowMenu(selectOptionText, -1, settings, filteredItems.ToArray());
            }
            else
            {
              CliTools.WriteLine(CliTools.WarnColor, "No matching items found, please try again or ESC to exit");
            }
          }
        }
      } while (true);
    }

    public static ConsoleModifiers LastModifiers { get; set; }

    public static bool ReadLineEx(out string result, string? defaultText = null, CancellationToken cancellationToken = default(CancellationToken), string? escapeText = null, bool isPswd = false)
    {
      StringBuilder stringBuilder = new StringBuilder(defaultText);
      var cancelled = false;
      var overrideMode = false;
      Task.Run(() =>
      {
        try
        {
          void WriteText(string text)
          {
            //Console.Write(isPswd ? new string('*', text.Length) : text); //this does not work with copying the chars
            if (isPswd)
            {
              ConsoleColor origBG = Console.BackgroundColor; // Store original values
              ConsoleColor origFG = Console.ForegroundColor;

              Console.BackgroundColor = ConsoleColor.DarkRed; // Set the block colour (could be anything)
              Console.ForegroundColor = ConsoleColor.DarkRed; // same as the background so it's not visible

              Console.Write(text);

              Console.BackgroundColor = origBG; // revert back to original
              Console.ForegroundColor = origFG;
            }
            else
            {
              Console.Write(text);
            }
          }

          var startingLeft = Console.CursorLeft;
          var startingTop = Console.CursorTop;
          ConsoleKeyInfo keyInfo;
          if (stringBuilder.Length > 0)
          {
            overrideMode = true;
            var saveFore = Console.ForegroundColor;
            var saveBack = Console.BackgroundColor;
            Console.ForegroundColor = saveBack;
            Console.BackgroundColor = saveFore;
            WriteText(stringBuilder.ToString());
            Console.ForegroundColor = saveFore;
            Console.BackgroundColor = saveBack;
          }
          var currentIndex = defaultText?.Length ?? 0;
          do
          {
            void SetCursorToCurrent()
            {
              var left = startingLeft + currentIndex % Console.BufferWidth;
              var top = startingTop + ((startingLeft + currentIndex) / Console.BufferWidth);
              Console.SetCursorPosition(left, top);
            }

            void DeleteCurrent()
            {
              Console.SetCursorPosition(startingLeft, startingTop);
              Console.Write(new string(' ', stringBuilder.Length));
            }
            void WriteCurrent()
            {
              Console.SetCursorPosition(startingLeft, startingTop);
              WriteText(stringBuilder.ToString());
              SetCursorToCurrent();
            }

            var previousLeft = Console.CursorLeft;
            var previousTop = Console.CursorTop;
            while (!Console.KeyAvailable)
            {
              cancellationToken.ThrowIfCancellationRequested();
              Thread.Sleep(50);
            }
            keyInfo = Console.ReadKey(true);

            switch (keyInfo.Key)
            {
              case ConsoleKey.A:
              case ConsoleKey.B:
              case ConsoleKey.C:
              case ConsoleKey.D:
              case ConsoleKey.E:
              case ConsoleKey.F:
              case ConsoleKey.G:
              case ConsoleKey.H:
              case ConsoleKey.I:
              case ConsoleKey.J:
              case ConsoleKey.K:
              case ConsoleKey.L:
              case ConsoleKey.M:
              case ConsoleKey.N:
              case ConsoleKey.O:
              case ConsoleKey.P:
              case ConsoleKey.Q:
              case ConsoleKey.R:
              case ConsoleKey.S:
              case ConsoleKey.T:
              case ConsoleKey.U:
              case ConsoleKey.V:
              case ConsoleKey.W:
              case ConsoleKey.X:
              case ConsoleKey.Y:
              case ConsoleKey.Z:
              case ConsoleKey.Spacebar:
              case ConsoleKey.Decimal:
              case ConsoleKey.Add:
              case ConsoleKey.Subtract:
              case ConsoleKey.Multiply:
              case ConsoleKey.Divide:
              case ConsoleKey.D0:
              case ConsoleKey.D1:
              case ConsoleKey.D2:
              case ConsoleKey.D3:
              case ConsoleKey.D4:
              case ConsoleKey.D5:
              case ConsoleKey.D6:
              case ConsoleKey.D7:
              case ConsoleKey.D8:
              case ConsoleKey.D9:
              case ConsoleKey.NumPad0:
              case ConsoleKey.NumPad1:
              case ConsoleKey.NumPad2:
              case ConsoleKey.NumPad3:
              case ConsoleKey.NumPad4:
              case ConsoleKey.NumPad5:
              case ConsoleKey.NumPad6:
              case ConsoleKey.NumPad7:
              case ConsoleKey.NumPad8:
              case ConsoleKey.NumPad9:
              case ConsoleKey.Oem1:
              case ConsoleKey.Oem102:
              case ConsoleKey.Oem2:
              case ConsoleKey.Oem3:
              case ConsoleKey.Oem4:
              case ConsoleKey.Oem5:
              case ConsoleKey.Oem6:
              case ConsoleKey.Oem7:
              case ConsoleKey.Oem8:
              case ConsoleKey.OemComma:
              case ConsoleKey.OemMinus:
              case ConsoleKey.OemPeriod:
              case ConsoleKey.OemPlus:
                if (keyInfo.KeyChar != '\0')
                {
                  if (overrideMode)
                  {
                    DeleteCurrent();
                    stringBuilder.Clear();
                    stringBuilder.Append(keyInfo.KeyChar);
                    currentIndex = 1;
                    WriteCurrent();
                    overrideMode = false;
                  }
                  else
                  {
                    stringBuilder.Insert(currentIndex, keyInfo.KeyChar);
                    currentIndex++;
                    WriteCurrent();
                  }
                }

                break;
              case ConsoleKey.Backspace:
                if (overrideMode)
                {
                  DeleteCurrent();
                  stringBuilder.Clear();
                  Console.SetCursorPosition(startingLeft, startingTop);
                  currentIndex = 0;
                  overrideMode = false;
                }
                else
                {
                  if (currentIndex > 0)
                  {
                    DeleteCurrent();
                    currentIndex--;
                    stringBuilder.Remove(currentIndex, 1);
                  }
                  WriteCurrent();
                }

                break;
              case ConsoleKey.Delete:
                if (overrideMode)
                {
                  DeleteCurrent();
                  stringBuilder.Clear();
                  Console.SetCursorPosition(startingLeft, startingTop);
                  currentIndex = 0;
                  overrideMode = false;
                }
                else
                {
                  if (stringBuilder.Length > currentIndex)
                  {
                    stringBuilder.Remove(currentIndex, 1);
                    Console.SetCursorPosition(previousLeft, previousTop);
                    WriteText(stringBuilder.ToString().Substring(currentIndex));
                    Console.Write(" ");
                    Console.SetCursorPosition(previousLeft, previousTop);
                  }
                  else
                    Console.SetCursorPosition(previousLeft, previousTop);
                }

                break;
              case ConsoleKey.LeftArrow:
                if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift)) //this does not work perfectly
                  break;
                if (overrideMode)
                {
                  currentIndex = 0;
                  overrideMode = false;
                }
                else
                {
                  if (currentIndex > 0)
                    currentIndex--;
                }
                SetCursorToCurrent();
                break;
              case ConsoleKey.RightArrow:
                if (keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift)) //this does not work perfectly
                  break;
                if (overrideMode)
                {
                  currentIndex = stringBuilder.Length;
                  overrideMode = false;
                }
                else
                {
                  if (currentIndex < stringBuilder.Length)
                    currentIndex++;
                }
                SetCursorToCurrent();
                break;
              case ConsoleKey.Home:
                overrideMode = false;
                currentIndex = 0;
                SetCursorToCurrent();
                break;
              case ConsoleKey.End:
                overrideMode = false;
                currentIndex = stringBuilder.Length;
                SetCursorToCurrent();
                break;
              //Not supported now, needs to implement 'Selection'
              //case ConsoleKey.Insert:
              //  overrideMode = !overrideMode;
              //  break;
              case ConsoleKey.Escape:
                //home:
                DeleteCurrent();
                currentIndex = 0;
                stringBuilder.Clear();
                stringBuilder.Insert(0, escapeText);
                WriteCurrent();
                cancelled = true;
                break;
              default:
                Console.SetCursorPosition(previousLeft, previousTop);
                break;
            }
          } while (keyInfo.Key != ConsoleKey.Enter && keyInfo.Key != ConsoleKey.Escape);

          LastModifiers = keyInfo.Modifiers;
          Console.WriteLine();
        }
        catch (OperationCanceledException)
        {
          //MARK: Change this based on your need. See description below.
          cancelled = true;
          stringBuilder.Clear();
        }
      }).Wait();
      result = stringBuilder.ToString();
      return !cancelled;
    }

    private static ConsoleKeyInfo ReadConsoleKey(params ConsoleKey[] validKeys)
    {
      do
      {
        var key = Console.ReadKey();
        if (validKeys.Contains(key.Key))
        {
          return key;
        }
      } while (true);
    }

    public static IDisposable Switch(this ConsoleColor color)
    {
      var saveColor = Console.ForegroundColor;
      Console.ForegroundColor = color;
      return Disposable.Create(() => Console.ForegroundColor = saveColor);
    }

    public static void EnterToContinue()
    {
      Console.WriteLine("\r\nHit <Enter> or <Esc> to continue.");
      ReadLineEx(out var _); //accepts also ESC
    }

    public static void Message(string? message)
    {
      Console.WriteLine(message);
      CliTools.EnterToContinue();
    }

    public static void ErrorMessage(string message, bool enterToContinue = true)
    {
      WriteLine(ErrorColor, message);
      if (enterToContinue)
        CliTools.EnterToContinue();
    }

    public static void WarnMessage(string? message, bool enterToContinue = true)
    {
      WriteLine(WarnColor, message);
      if (enterToContinue)
        CliTools.EnterToContinue();
    }

    public static void SuccessMessage(string? message, bool enterToContinue = true)
    {
      WriteLine(OkColor, message);
      if (enterToContinue)
        CliTools.EnterToContinue();
    }

    public static void WriteLine(string? message)
    {
      Console.WriteLine(message);
    }

    public static void WriteLine(ConsoleColor color, string? message)
    {
      using (color.Switch())
      {
        Console.WriteLine(message);
      }
    }

    public static void WriteLine((bool success, string? message) messageWithSuccessFlag)
    {
      WriteLine(messageWithSuccessFlag.success ? OkColor : NotOkColor, messageWithSuccessFlag.message);
    }

    public static void WriteLine(bool success, string? successMessage, string? nonSuccessMessage)
    {
      WriteLine(success ? OkColor : NotOkColor, success ? successMessage : nonSuccessMessage);
    }

    public static void Write(ConsoleColor color, string message)
    {
      using (color.Switch())
      {
        Console.Write(message);
      }
    }

    public static void WriteTitle(string? message)
    {
      if (message == null)
        return;
      WriteLine(ConsoleColor.White, message);
      Console.WriteLine(new string('=', message.Length) + Environment.NewLine);
    }

    public static void WriteSubTitle(string? message)
    {
      WriteLine(ConsoleColor.White, message);
    }

    public static bool ConfirmWith1OrBack(string message)
    {
      CliTools.WriteLine(TitleColor, "Confirmation");
      return (bool)ShowMenu(null,
        new MenuItem(message, () => { }) { Tag = true },
        new MenuItem("Esc", "<- Back", () => { }) { Tag = false }).Tag!;
    }

    public static bool ConfirmWithEnterOrBack(string message)
    {
      CliTools.WriteLine(TitleColor, "Confirmation:");
      Console.WriteLine(" " + message);
      CliTools.Write(MenuSelectColor, "<Enter>");
      Console.Write(" to continue");
      CliTools.Write(MenuSelectColor, " <Esc>");
      Console.Write(" to cancel ");
      return ReadLineEx(out var _);
    }

    public static bool ConfirmAdminRestart()
    {
      return ConfirmWithEnterOrBack("Application needs administrative privileges - Restart?");
    }

    public static bool BooleanQuestion(string text, out bool value, bool? defaultAnswer = null)
    {
      var result = BooleanQuestion(text, defaultAnswer);
      if (result.HasValue)
        value = result.Value;
      else
        value = false;
      return result.HasValue;
    }

    public static bool? BooleanQuestion(string text, bool? defaultAnswer = null)
    {
      try
      {
        while (true)
        {
          Console.Write(text);
          CliTools.Write(MenuSelectColor, " [y|n|Esc] ");
          
          string? defaultText = null;
          if (defaultAnswer.HasValue)
            defaultText = defaultAnswer.Value ? "y" : "n";

          ReadLineEx(out var input, escapeText: "Esc", defaultText: defaultText);
          if (input != null)
          {
            if (input.Trim().ToLower() == "esc")
              return null;
            if (input.Trim().ToLower() == "y")
              return true;
            if (input.Trim().ToLower() == "n")
              return false;
          }
        }
      }
      finally
      {
        Console.WriteLine();
      }

      //single key approach (is not consistent to other items/menus)
      //var key = ReadConsoleKey(ConsoleKey.Y, ConsoleKey.N, ConsoleKey.Escape);
      //try
      //{
      //  switch (key.Key)
      //  {
      //    case ConsoleKey.Y:
      //      return true;
      //    case ConsoleKey.N:
      //      return false;
      //    case ConsoleKey.Escape:
      //      return null;
      //    default:
      //      throw new InvalidOperationException($"Invalid Key: {key.Key}");
      //  }
      //}
      //finally
      //{
      //  Console.WriteLine();
      //}
    }

    public static bool ConfirmOverwrite(string filename)
    {
      Console.WriteLine($"File already exists: {filename}");
      return BooleanQuestion("Overwrite file?").GetValueOrDefault();
    }

    public static string? InputQuery(string hintText, string? defaultText = null)
    {
      if (InputQuery(hintText, out var result, defaultText))
        return result;
      return null;
    }

    public static bool InputQuery(string hintText, out string value, string? defaultText = null)
    {
      Console.Write(hintText + " (");
      Write(MenuSelectColor, "Esc");
      Console.Write(" to cancel):");
      return ReadLineEx(out value, defaultText);
    }

    public static bool InputQueryInt(string hintText, [NotNullWhen(true)] out int? value, int? defaultValue = null)
    {
      var defaultText = defaultValue?.ToString();
      value = null;
      do
      {
        if (!InputQuery(hintText, out var res, defaultText))
          return false;

        if (int.TryParse(res, out var parsedValue))
        {
          value = parsedValue;
          return true;
        }
      } while (true);
    }

    public static bool InputQueryPswd(string text, [NotNullWhen(true)] out string? password, bool allowEmptyPassword, string? defaultPassword = null)
    {     
      if (!string.IsNullOrWhiteSpace(text))
        Console.WriteLine(text);

      var result = ReadLineEx(out password, defaultPassword, CancellationToken.None, isPswd:true);
      if (!result)
        password = null;
      if (string.IsNullOrEmpty(password) && !allowEmptyPassword)
        return false;
      return result;
    }

    public static bool InputQueryPswdForItem(string item, [NotNullWhen(true)] out string? password, bool allowEmptyPassword, string? defaultPassword = null)
    {
      return InputQueryPswd($"Enter password for {item}", out password, allowEmptyPassword, defaultPassword);
    }

    public static bool SelectItem<T>(IEnumerable<T> items, string selectOptionText, T defaultValue, [NotNullWhen(true)] out T? result, Func<T, string?> textFunc, MenuSettings? settings = null)
    {
      var menuItems = BuildMenuItemsWithText(items, textFunc);
      return SelectMenuItem(selectOptionText, menuItems, defaultValue, out result, settings);
    }

    public static bool SelectItem<T, TResult>(IEnumerable<T> items, string selectOptionText, T defaultValue, [NotNullWhen(true)] out TResult? result, 
      Func<T, string?> textFunc, Func<T, TResult> selector, MenuSettings? settings = null)
    {
      var menuItems = BuildMenuItemsWithText(items, textFunc);
      var success = SelectMenuItem(selectOptionText, menuItems, defaultValue, out var res, settings);
      if (success)
        result = selector(res!);
      else
        result = default;
      return success;
    }

    public static bool SelectMultipleItems<T>(IEnumerable<T> items, bool defaultSelected, string selectOptionText, [NotNullWhen(true)] out List<T>? selectedItems, Func<T, string?> textFunc, MenuSettings? settings = null)
    {
      var selectableTages = items.Select(e => new SelectableTag<T>(defaultSelected, e)).ToList();
      var result = SelectMultipleItems(selectableTages, selectOptionText, textFunc, settings);
      if (result)
      {
        selectedItems = selectableTages
          .Where(e => e.Selected)
          .Select(e => e.Value)
          .ToList();
      }
      else
      {
        selectedItems = null;
      }
      return result;
    }

    public static bool SelectMultipleItems<T>(List<SelectableTag<T>> items, string selectOptionText, Func<T, string?> textFunc, MenuSettings? settings = null)
    {
      while (true)
      {
        var escItem = new MenuItem("Esc", "Cancel", null);
        var menuItems = new List<MenuItem>();
        foreach (var selectableTag in items)
        {
          menuItems.AddCheckBox(null, textFunc(selectableTag.Value) ?? string.Empty, selectableTag);
        }
        menuItems.AddSeparator("--- selection functions ---");
        menuItems.Add("S", "Select all items", () =>
        {
          foreach (var selectableTag in items)
            selectableTag.Selected = true;
        });
        menuItems.Add("D", "Deselect all items", () =>
        {
          foreach (var selectableTag in items)
            selectableTag.Selected = false;
        });
        menuItems.Add("T", "Toggle selection of all items", () =>
        {
          foreach (var selectableTag in items)
            selectableTag.Selected = !selectableTag.Selected;
        });
        var okItem = new MenuItem("OK", "Use this selection", null);
        menuItems.Add(okItem);
        menuItems.Add(escItem);

        var defaultIndex = -1;
        var selected = ShowMenu(selectOptionText, defaultIndex, settings ?? new MenuSettings(), menuItems.ToArray());
        if (selected == escItem)
        {
          return false;
        }
        if (selected == okItem)
        {
          return true;
        }
      }
    }

    public static List<MenuItem> BuildMenuItemsWithText<T>(IEnumerable<T> items, Func<T, string?> textFunc)
    {
      return items
        .Select(e => new MenuItem(textFunc(e)??string.Empty) {Tag = e})
        .ToList();
    }


    public static bool SelectMenuItem<T>(string selectOptionText, List<MenuItem> menuItems, T defaultValue, out T? result, MenuSettings? settings = null)
    {
      var escItem = new MenuItem("Esc", "Cancel", null);
      menuItems.Add(escItem);

      var defaultIndex = defaultValue != null
        ? menuItems.Select(e => e.Tag).ToList().FindIndex(e => object.Equals(defaultValue, e))
        : -1;

      var selected = ShowMenu(selectOptionText, defaultIndex, settings ?? new MenuSettings(), menuItems.ToArray());
      if (selected == escItem)
      {
        result = default(T);
        return false;
      }
      else
      {
        result = (T?)selected.Tag;
        return true;
      }
    }

    public static MenuItem Add(this ICollection<MenuItem> menuItems, string key, string text, Action? action,
      Func<bool?>? canExecuteFunc = null, bool adminRequired = false, object? tag = null)
    {
      var menuItem = new MenuItem(key, text, action, canExecuteFunc) {AdminRequired = adminRequired, Tag = tag};
      menuItems.Add(menuItem);
      return menuItem;
    }
    public static MenuItem Add(this ICollection<MenuItem> menuItems, string text, Action? action,
      Func<bool?>? canExecuteFunc = null, bool adminRequired = false, object? tag = null)
    {
      var menuItem = new MenuItem(text, action, canExecuteFunc) {AdminRequired = adminRequired, Tag = tag};
      menuItems.Add(menuItem);
      return menuItem;
    }

    public static void AddSeparator(this ICollection<MenuItem> menuItems, string text)
    {
      menuItems.Add(new MenuSeparator(text));
    }

    public static MenuItem AddCheckBox(this ICollection<MenuItem> menuItems, string? key, string text, bool state, 
      Action<bool> changeAction, Func<bool?>? canExecuteFunc = null)
    {
      var menuItem = new MenuItem(key, (state ? "[X] " : "[ ] ") + text, () => changeAction(!state), canExecuteFunc);
      menuItems.Add(menuItem);
      return menuItem;
    }

    public static MenuItem AddCheckBoxNullable(this ICollection<MenuItem> menuItems, string? key, string text, bool? state, 
      Action<bool?> changeAction, Func<bool?>? canExecuteFunc = null)
    {
      var menuItem = new MenuItem(key, (state == true ? "[X] " : state == false ? "[ ] " : "[?] ") + text, () =>
      {
        switch (state)
        {
          case true:
            changeAction(null);
            break;
          case false:
            changeAction(true);
            break;
          case null:
            changeAction(false);
            break;
        }
      }, canExecuteFunc);
      menuItems.Add(menuItem);
      return menuItem;
    }

    public static MenuItem AddCheckBox<T>(this ICollection<MenuItem> menuItems, string? key, string text, SelectableTag<T> tag,
      Func<SelectableTag<T>, bool>? shallFlipState = null, Func<bool?>? canExecuteFunc = null)
    {
      
      var mi = new MenuItem(key, string.Empty, null, canExecuteFunc);

      void SetState(bool state)
      {
        mi.Text = (state ? "[X] " : "[ ] ") + text;
      }

      SetState(tag.Selected);
      mi.Tag = tag;
      mi.Action = () =>
      {
        if (shallFlipState?.Invoke(tag) ?? true)
        {
          tag.Selected = !tag.Selected;
          SetState(tag.Selected);
        }
      };
      menuItems.Add(mi);
      return mi;
    }

    public class SelectableTag<T>
    {
      public SelectableTag(bool selected, T value)
      {
        Selected = selected;
        Value = value;
      }

      public bool Selected { get; set; }
      public T Value { get; set; }
    }

    #region Markup
   
    //https://regex101.com/r/NlXO7o/1  https://regex101.com/delete/VAY9Y3LtsxjTj76UOE25T7vHFZmSjy1JC4Dq
    [GeneratedRegex(@"(?<Items>((?<Fore>[a-zA-Z]+)|_(?<Back>[a-zA-Z]+)|(?:[,\s]*)?))+", RegexOptions.Compiled)]
    private static partial Regex ColorsRegEx();

    /// <summary>
    /// Use this to include colors in output
    /// Use these Color names, use '_' to set BackgroundColor Example "normal [red,_yellow]RedOnYellowText[/]restored normal"
    ///  Black = 0,
    ///  DarkBlue = 1,
    ///  DarkGreen = 2,
    ///  DarkCyan = 3,
    ///  DarkRed = 4,
    ///  DarkMagenta = 5,
    ///  DarkYellow = 6,
    ///  Gray = 7,
    ///  DarkGray = 8,
    ///  Blue = 9,
    ///  Green = 10,
    ///  Cyan = 11,
    ///  Red = 12,
    ///  Magenta = 13,
    ///  Yellow = 14,
    ///  White = 15
    /// </summary>
    /// <param name="text"></param>
    public static void MarkupLine(string text)
    {
      Markup(text);
      Console.WriteLine();
    }

    public static List<(bool isMarkup, string value)> ParseMarkup(string text)
    {
      var s = text.AsSpan();
      var result = new List<(bool isMarkup, string value)>();
      bool isMarkup = false;
      while (s.Length > 0)
      {
        var idx = s.IndexOfAny("[]");
        if (idx < 0)
        {
          result.Add((isMarkup, s.ToString()));
          return result;
        }
        //detect double brackets (and not triple brackets)
        if (s.Length > (idx + 1) && s[idx] == s[idx + 1] && !(isMarkup && s.Length > (idx + 2) && s[idx] == s[idx + 2]))
        {
          idx++; //skip one of the doubles
          result.Add((isMarkup, s.Slice(0, idx).ToString()));
        }
        else
        { //single bracket
          if (idx > 0)
            result.Add((isMarkup, s.Slice(0, idx).ToString()));
          if (s[idx] == '[' && !isMarkup)
            isMarkup = true;
          else if (s[idx] == ']' && isMarkup)
            isMarkup = false;
        }
        s = s.Slice(idx + 1);
      }

      return result;
    }

    public static Dictionary<string, ConsoleColor> CustomColorNames = new Dictionary<string, ConsoleColor>(StringComparer.OrdinalIgnoreCase);

    public static void Markup(string text)
    {
      var tokens = ParseMarkup(text);
      if (!tokens.Any())
        return;

      var savedColors = new Stack<(ConsoleColor fore, ConsoleColor back)>();
      
      void Push()
      {
        savedColors!.Push((Console.ForegroundColor, Console.BackgroundColor));
      }

      void Pop()
      {
        if (savedColors.Count > 0)
        {
          var colors = savedColors.Pop();
          Console.ForegroundColor = colors.fore;
          Console.BackgroundColor = colors.back;
        }
      }

      foreach (var (isMarkup, value) in tokens)
      {
        if (isMarkup)
        {
          if (value == "/")
          {
            Pop();
          }
          else
          {
            bool hasBeenUsed = false;
            var colorMatch = ColorsRegEx().Match(value);
            if (colorMatch.Success)
            {
              var foreCapture = colorMatch.Groups["Fore"].Captures.OfType<Capture>().LastOrDefault();
              var backCapture = colorMatch.Groups["Back"].Captures.OfType<Capture>().LastOrDefault();
              if (foreCapture != null || backCapture != null)
              {
                Push();
                if (foreCapture != null)
                {
                  if (Enum.TryParse<ConsoleColor>(foreCapture.Value, out var color) ||
                      CustomColorNames.TryGetValue(foreCapture.Value, out color))
                  {
                    Console.ForegroundColor = color;
                    hasBeenUsed = true;
                  }
                }

                if (backCapture != null)
                {
                  if (Enum.TryParse<ConsoleColor>(backCapture.Value, out var color) ||
                      CustomColorNames.TryGetValue(backCapture.Value, out color))
                  {
                    Console.BackgroundColor = color;
                    hasBeenUsed = true;
                  }
                }
              }
            }

            if (!hasBeenUsed)
            {
              Console.Write($"[{value}]");
            }
          }
        }
        else
        {
          Console.Write(value);
        }
      }

      while (savedColors.Any())
        Pop();
    }

    #endregion
  }
}
