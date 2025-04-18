# Commandline uasge of LgpCli
LgpCli is a hybrid application:<br>
- it can run in interactive UI Mode - just start it without parameters
- it can run as batch tool, run from commandline with parameters

## General info
Use `LgpCli.exe /?` to get the general help
```
Description:
  Cli tool to manage local group policies © 2025, Bernd Klaiber

Usage:
  LgpCli [command] [options]

Options:
  --version       Show version information
  -?, -h, --help  Show help and usage information

Commands:
  interactive                                Shows the commandline UI.
  enable <policy> <Both|Machine|User>        sets a policy to enabled/active state
  disable <policy> <Both|Machine|User>       sets a policy to disable state
  notconfigure <policy> <Both|Machine|User>  sets a policy to not configured state
  get-state <policy> <Both|Machine|User>     gets a policy's state
  search <Both|Machine|User>                 searches for policies
  batch <batchfile>                          process a batch file
  ```
If you need to provide a policy, the commandline expects an identifier like `windows.ExplorerRibbonStartsMinimized`. This a category and the internal name of the policy. The categoty is not exactly the name in the tree, but quite similar. The name is the internal ID, defined in the admx-files. The app usually marks these idents in yellow (like <span style="color:yellow;">windows.ExplorerRibbonStartsMinimized</span>). The app always shows this ID, so it won't be a problem to find it.

When specifying policy, you also need to provide the scope ('class') **machine** or the **user** configuration. So the class of a policy can be machine or user. (class is usually marked as light blue in LgpCli like <span style="color:skyblue;">machine</span>)

When LgpCli reports the state of a policy, there might be the value <span style="color:red;">suspect</span>. This is not necessarily an error. This just is an indicator, that not all values of the policy have an expected state. Sometimes multiple policies are setting overlapping different values. So don't be scary here. You can also check the current state using GpEdit.msc<br>
NOTE:<br>
GPEdit does not show changed values instantly. You need to **RESTART** GPEdit to see changed policies values.
## `LgpCli interactive /?`
```
Description:
  Shows the commandline UI.

Usage:
  LgpCli interactive [command] [options]

Commands:
  show <policy> <Both|Machine|User>  Shows a policy.
```  
There is currently only one command available in this section:
### `LgpCli interactive show /?`
This will start the interactive mode and directly jump to a policy. Was just useful for testing.
```
Description:
  Shows a policy.

Usage:
  LgpCli interactive show [<policy> [<policyclass>]] [options]

Arguments:
  <policy>             Prefixed name of a policy.
  <Both|Machine|User>  Context for a policy.

Options:
  -?, -h, --help  Show help and usage information
```

## `LgpCli enable /?`
enables a single policy
```
Description:
  sets a policy to enabled/active state

Usage:
  LgpCli enable [<policy> [<policyclass>]] [options]

Arguments:
  <policy>             Prefixed name of a policy.
  <Both|Machine|User>  Context for a policy.

Options:
  -k, --key <key>                            name for an element value for setting a policy.
  -v, --value <value>                        value for an element value for setting a policy.
  -gs, --get-state <After|Before|Both|None>  reports also the state before or after setting it [default: Both]
  -?, -h, --help                             Show help and usage information
```
if the policy has values to define, you need to specify them as pairs of key and value. The key is shown in the policy (typical in blue like <span style="color:blue;">ExplorerRibbonStartsMinimizedDropdown</span>)<br>
Example:<br>
`LgpCli enable windows.ExplorerRibbonStartsMinimized User -k ExplorerRibbonStartsMinimizedDropdown -v ExplorerRibbonStartsMinimized_StartsNotMinimized`<br>
There might be multiple pairs if the policy takes more than one value. In special cases, there are also multiple values allowed (list values).<br>
Don't bother with that syntax, just use the option 'B' in the policy-view to build the syntax for the current policy. Use 'M' to modify the values according to your needs first (go back with 'Esc').<br>
You can check the state before and after applying the policy, use `-gs both` which is the default, use 'none' to skip these checks.

NOTE:<br>
GPEdit does not show changed values instantly. You need to **RESTART** GPEdit to see changed policy states/values in GPEdit.
## `LgpCli disable /?`
Sets a policy tp 'Disabled' state, see 'enable' for more info.
```
Description:
  sets a policy to disable state

Usage:
  LgpCli disable [<policy> [<policyclass>]] [options]

Arguments:
  <policy>             Prefixed name of a policy.
  <Both|Machine|User>  Context for a policy.

Options:
  -gs, --get-state <After|Before|Both|None>  reports also the state before or after setting it [default: Both]
  -?, -h, --help                             Show help and usage information
```

## `LgpCli notconfigure /?`
Sets a policy to 'Not Configured' state, see 'enable' for more info.
```
Description:
  sets a policy to not configured state

Usage:
  LgpCli notconfigure [<policy> [<policyclass>]] [options]

Arguments:
  <policy>             Prefixed name of a policy.
  <Both|Machine|User>  Context for a policy.

Options:
  -gs, --get-state <After|Before|Both|None>  reports also the state before or after setting it [default: Both]
  -?, -h, --help                             Show help and usage information
```

## `LgpCli get-state /?`
Just reports the current state of a policy.
```
Description:
  gets a policy's state

Usage:
  LgpCli get-state [<policy> [<policyclass>]] [options]

Arguments:
  <policy>             Prefixed name of a policy.
  <Both|Machine|User>  Context for a policy.

Options:
  -?, -h, --help  Show help and usage information
```
## `LgpCli batch /?`
Use this option to apply multiple policy states/values in one run. **First** use 'M' from main screen to initialize a command file. Then use 'B' in the policy view to generate the batch command. If there is a commandfile defined, the app will ask you to add the command to that file.
![Creating batch file](CreateBatchFile.png)
```
Description:
  process a batch file

Usage:
  LgpCli batch <batchfile> [options]

Arguments:
  <batchfile>  a file with batch commands to process.

Options:
  -coe, --continue-on-error  continue batch processing on error. [default: False]
  -?, -h, --help             Show help and usage information
```
