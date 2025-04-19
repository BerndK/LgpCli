# LgpCli - Local Group policy CommandLine Interface
This is a tool to manage your local group policies. 

[![Screencast of LgpCli Main workflow](./doc/SearchAndSetPolicy.gif)](./doc/SearchAndSetPolicy.mp4 "Click to see video pause/stoppable")

It can:
 - apply / modify policies
 - search for policies
 - runs as an interactive CLI tool with UI (non graphical)
 - runs also as a traditional cmd-tool for automation (batch mode)
 - can create the batch syntax for you using the UI
 
It is not: 
 - not a tool for beginner
 - should not replace GPEdit
 - should not be used in AD context (domain joined systems)

 [What are policies](./doc/LocalGroupPolicy.md)<br>
 [Commandline interface](./doc/Commandline.md)

 This shows the workflow of first using the UI to define the batch command, then applying the policy state 'enabled' using the command-line:
 ![full workflow](./doc/SetPolicyCmdline.gif)

 # ==Important Notes==
 - Using this tool may harm your system by setting unwanted settings in the registry. Use it with care and only if you know what you are doing.
 - The author is not liable for any problems that may arise in connection with the use of this tool.
 - There will be no support, especially not for the policies itself. If you want to achieve a dedicated behavior, first test this using the windows tool GPEdit.msc. Don't come up with questions like "I set policy Xyz, the expected behavior is not happening"
 - Many policies only show up effective after a reboot, or e.g. a restart of the explorer process (when looking at the policy in this documentation) 
 - GPEdit does not show changed values instantly. You need to **RESTART** GPEdit to see changed policy states/values in GPEdit.
