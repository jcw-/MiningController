-----

####Build Status

| CI (Stable) | CI (Unstable)
| :-------------: | :-------------: |
| ![stable](https://ci.appveyor.com/api/projects/status/2irfgpw9gaqw7yfu) | ![unstable](https://ci.appveyor.com/api/projects/status/ecbxls15udo416xt) |

-----

Mining Controller
-----

An sgminer/cgminer controller for your Windows computer.


####About

Do you want to mine a cryptocurrency on your computer but are concerned with the graphics lag that is introduced?

This .NET 4.5 app does not replace your miner program, but it will control it, throttling it down while your computer is in use, and even pausing mining while certain programs (such as games) are open. When your computer is idle, it will cause the miner to mine at your maximum settings.

In addition, it acts as a watchdog, ensure the miner is running when it is supposed to, and even allows you to hide the miner window if you prefer.


####Supported Platforms


Tested on Windows 7 against cgminer 3.7.2 and sgminer 4.1.0.


####Getting Started

See [Getting Started][gs] for configuration details, including how to automatically launch Mining Controller when your computer starts up.


####Features

 - Reduces mining intensity when computer is in use
 - Pauses mining while important processes (configured by the user) are running
 - Automatically launches mining program
 - Snooze feature with configurable durations
 - Ability to show or hide the miner window
 - Ability to minimize to tray
 - Displays up to seven days of hashrate history in a graph
 - Tray tooltip includes current hashrate and graph


####Screenshots

![screenshot1](http://jcw-.github.io/MiningController/images/screenshots/Screenshot1.png)

![screenshot2](http://jcw-.github.io/MiningController/images/screenshots/Screenshot2.png)

![screenshot3](http://jcw-.github.io/MiningController/images/screenshots/Screenshot3.png)

![tray hover](http://jcw-.github.io/MiningController/images/screenshots/TrayHover.png)


####Problems?

If you find an issue, please visit the [issue tracker][issues] and report it.


####License

[MIT License][license]


####Development Documentation

https://github.com/jcw-/MiningController/wiki/


####How to Contribute

Contributions are welcome! Submit an [issue][issues] or become familiar with the [development documentation][dev] and submit a pull request. 


####Credits

See [NOTICE][notice].

[gs]: https://github.com/jcw-/MiningController/wiki/Getting-Started
[dev]: https://github.com/jcw-/MiningController/wiki/
[notice]: ./NOTICE
[license]: ./LICENSE
[issues]: https://github.com/jcw-/MiningController/issues
