Name: Power Manager
Summary: Run your home's power from one panel on a HUD button: start and stop generators, set their auto-start, and refuel them from storage in your own fuel order, junk paper first. A battery forecast shows how long power lasts, low-priority devices switch off when the battery runs low in a power cut, and auto-refuel keeps generators fed.

Description (BBCode):
[center][img]https://sldb.core-directive.com/media/save/1-save-safe.png[/img][/center]

Running your home's power means walking to each generator to start it, stop it and feed it fuel, and guessing how long the battery will last. Power Manager puts it all in one panel you can open from anywhere.

[size=5]Main features[/size]
[list]
[*][b]Battery forecast[/b]: how much the battery holds, what your home makes and uses, and how long until it is empty or full.[/*]
[*][b]Generators[/b]: every fuel generator with its fuel time left. [b]Start[/b], [b]Stop[/b] and [b]Refuel[/b] each one, or [b]Start all[/b], [b]Stop all[/b] and [b]Refuel all[/b].[/*]
[*][b]Auto-start[/b]: turn on the game's own auto-start for a generator and pick the battery level it starts below, for one generator or all of them.[/*]
[*][b]Refuel from home storage[/b]: fuel comes out of your home's storage, in your fuel order. No walking.[/*]
[*][b]Fuel order[/b]: the Fuel tab lists the items burned as fuel, top first, with how many you have. Each item shows its burn value. Move them up and down, remove them, or add any other item that burns, whether you have it or not; an item you remove can be added back right away. Items left off the list are never burned. The default burns notes, letters, flyers and other junk paper first, then Poor-Quality, Common and High-Proof Alcohol, then Diesel last.[/*]
[*][b]Auto-refuel[/b]: an option, off by default, that tops up a running generator, or one set to auto-start, when it has less than 60 in-game minutes of fuel left. The minutes can be changed.[/*]
[*][b]Devices[/b]: every powered device at home with its power use and an On/Off switch.[/*]
[*][b]Load shedding[/b]: mark devices as low priority. During a power cut they switch off when the battery falls below 20%, and back on once it is 10 points above. The level can be changed.[/*]
[*][b]Power and solar settings[/b]: battery size, device power use, when the city power is cut or that it never is, Blackout panic, solar output and output in bad weather, generator power and fuel burn. These used to be in Game Rules and Machine Speed; values you set there are carried over the first time the game starts with Power Manager.[/*]
[*][b]HUD button[/b]: a lightning bolt at the top of the bottom-left tool column opens the panel.[/*]
[*][b]Messages[/b]: a line after each press says what happened. With the panel closed, auto-refuel and load shedding messages show as pop-ups.[/*]
[*][b]UI size[/b]: the mod's own UiScale setting sizes the panel, or it follows Exiled's Toolkit's UI size.[/*]
[/list]

[size=5]Hotkeys[/size]
[list]
[*][b]Esc[/b]: close the panel.[/*]
[/list]
The panel key is unbound by default: the HUD button opens the panel. Set PanelKey in the config to open it from the keyboard too; it does nothing while you type in a text box. The game's own hotkeys are off while the panel is open.

Every action is the game's own: the same start, stop, auto-start, refuel and switch the game's menus use, and refuelling uses the items up. The mod writes nothing into your save file. You can remove it at any time; generators and devices stay as they are.

Works in English and Chinese.

[size=5]Known issues[/size]
[list]
[*]Stopping a generator also turns its auto-start off. This is the game's own rule; turn auto-start back on in the panel.[/*]
[*]While the game holds its power switches during some story events, nothing can be switched.[/*]
[*]Devices that Defense Manager powers are shown but cannot be switched or marked low priority here.[/*]
[*]A low-priority device you switch back on while the battery is still low is switched off again. Mark it normal priority to keep it on.[/*]
[*]Device priorities are kept per save, so a new game must be saved once before devices can be marked low priority.[/*]
[*]A change to the battery size takes effect at the next game start.[/*]
[/list]

[center][img]https://sldb.core-directive.com/img/mods-banner.jpg[/img]
[size=4][url=https://www.nexusmods.com/survivallog/mods/73]Get Mod Manager[/url] to set up all your mods in game  |  [url=https://sldb.core-directive.com/en/]Open the Survival Log wiki[/url] for items, recipes and maps[/size][/center]

[size=5]Requirements[/size]
[list]
[*]Survival Log 1.1.20151. A game update can break it until the mod is updated.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]. Install it first.[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.12.0 or newer. Power Manager does not load without it. It keeps the panel key quiet while you type, shows messages as pop-ups while the panel is closed and applies settings changed on the Mod Manager's Mods screen without a restart.[/*]
[/list]

[size=5]Installation[/size]
[list=1]
[*]Install [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i](if no other mods were installed before, start the game once so BepInEx finishes setup, then quit)[/i].[/*]
[*]Install Exiled's Toolkit the same way. Download its zip and extract it into the game folder.[/*]
[*]Download the zip from the Files tab.[/*]
[*]Extract it into the game folder (the folder with [font=Courier New]SurvivalLog.exe[/font]). The zip holds a BepInEx folder, so the DLL lands in the right place. When asked, merge the folders. The result should be:
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\PowerManager.dll[/code][/*]
[/list]
If you use Game Rules or Machine Speed, update them to 0.7.0 or newer too: their power settings have moved here. Until you do, Power Manager leaves those settings to the old version and a message asks you to update it.

[size=5]Uninstall[/size]
Delete [font=Courier New]PowerManager.dll[/font] from the [font=Courier New]BepInEx\plugins[/font] folder. Devices the mod switched off to save the battery stay off; switch them on from the game's own power panel.

[size=5]Configuration[/size]
Auto-refuel, its minutes, the load shedding level and the fuel order can be changed in the panel. Everything else, such as the key and the power and solar settings, can be changed in game on the Mod Manager's Mods screen, where changes take effect at once (the battery size at the next game start), or in [font=Courier New]BepInEx\config\com.dev.survivallog.powermanager.cfg[/font] while the game is closed (see [url=https://github.com/AnExiledDev/SurvivalLog-PowerManager/blob/main/CONFIG.md]CONFIG.md[/url]; the cfg file explains each setting in English and Chinese).

[size=5]Feedback[/size]
Found a bug or have an idea? Come say hi on the [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url], or post in this page's Posts tab. For bugs, please include [font=Courier New]BepInEx\LogOutput.log[/font] and [font=Courier New]BepInEx\power-manager.log[/font].
To reach me directly, message [b]exiled.dev[/b] on Discord; you can find me in the [url=https://discord.gg/MZGg4gQAUe]official Survival Log Discord[/url]. I'm also on X as [url=https://x.com/AnExiledDev]@AnExiledDev[/url].

[size=5]Source and license[/size]
Open source under MIT: [url=https://github.com/AnExiledDev/SurvivalLog-PowerManager]github.com/AnExiledDev/SurvivalLog-PowerManager[/url]

[line]
[size=5]简体中文[/size]

管理家里的电力，意味着要走到每台发电机跟前启动、关闭、添加燃料，还要猜蓄电池能撑多久。Power Manager 把这些都放进一个面板，在哪里都能打开。

[size=5]主要功能[/size]
[list]
[*][b]电量预测[/b]：蓄电池储存了多少电，家里发电和用电各多少，以及还要多久耗尽或充满。[/*]
[*][b]发电机[/b]：列出每台燃油发电机和剩余燃料时间。可以单独「启动」「关闭」「加燃料」，也可以「全部启动」「全部关闭」「全部加燃料」。[/*]
[*][b]自动启动[/b]：为发电机开启游戏自带的自动开启，并选择电量低于多少时启动，可以单独设置，也可以全部一起设置。[/*]
[*][b]从家中储物加燃料[/b]：燃料按你的燃料顺序从家里的储物中取用，不用走过去。[/*]
[*][b]燃料顺序[/b]：「燃料」标签页从上往下列出当作燃料的物品，以及你有多少。每个物品都会显示燃烧值。可以上移、下移、移除，或添加其他任何可燃物品，不管你有没有；移除的物品可以马上再加回来。不在列表中的物品永远不会被烧掉。默认先烧便条、信件、传单等没用的纸，再依次烧劣质酒精、普通酒精、烈性酒精，最后烧柴油。[/*]
[*][b]自动加燃料[/b]：一个默认关闭的选项，开启后运行中或设为自动启动的发电机剩余燃料少于 60 游戏分钟时会自动加满。分钟数可以修改。[/*]
[*][b]用电设备[/b]：列出家里每个用电设备的耗电量和开关。[/*]
[*][b]负载削减[/b]：把设备标为低优先级。停电期间电量低于 20% 时它们会关闭，回升 10 个百分点后重新开启。这个电量可以修改。[/*]
[*][b]电力和太阳能设置[/b]：蓄电池容量、设备耗电、市电何时切断或永不切断、「停电恐慌」、太阳能板发电量和恶劣天气下的发电量、发电机发电量和燃料消耗速度。这些设置原本在 Game Rules 和 Machine Speed 中；你在那里设置的数值会在首次带着 Power Manager 启动游戏时自动沿用。[/*]
[*][b]HUD 按钮[/b]：左下角工具栏最上方的闪电按钮可以打开面板。[/*]
[*][b]提示信息[/b]：每次按下按钮后会有一行说明结果。面板关闭时，自动加燃料和负载削减的提示会以弹出通知显示。[/*]
[*][b]界面大小[/b]：本模组自己的 UiScale 设置可以调整面板大小，也可以跟随 Exiled's Toolkit 的界面大小。[/*]
[/list]

[size=5]快捷键[/size]
[list]
[*][b]Esc[/b]：关闭面板。[/*]
[/list]
面板按键默认不绑定，用 HUD 按钮打开面板。在配置中设置 PanelKey 后也可以用键盘打开；在输入框里打字时按键不起作用。面板打开时游戏自己的快捷键不起作用。

所有操作都是游戏自己的：和游戏菜单里的启动、关闭、自动开启、加燃料和开关完全一样，加燃料也会真的消耗物品。模组不会往存档里写入任何数据。你可以随时移除它；发电机和设备保持原样。

支持英文和中文。

[size=5]已知问题[/size]
[list]
[*]关闭发电机时，它的自动启动也会关闭。这是游戏自己的规则，可以在面板里重新开启。[/*]
[*]某些剧情事件期间游戏会锁定电源开关，此时无法切换任何东西。[/*]
[*]由 Defense Manager 控制供电的设备会显示出来，但不能在这里切换或标为低优先级。[/*]
[*]电量仍然偏低时，你手动重新打开的低优先级设备会再次被关闭。想让它保持开启，把它改回普通优先级。[/*]
[*]设备优先级按存档保存，所以新游戏要先保存一次才能标记低优先级设备。[/*]
[*]蓄电池容量的修改在下次启动游戏时生效。[/*]
[/list]

[url=https://www.nexusmods.com/survivallog/mods/73]获取 Mod Manager[/url]，在游戏里设置所有模组。[url=https://sldb.core-directive.com/zh/]打开 Survival Log 维基[/url]，物品、配方、地图一查就有。

[size=5]前置要求[/size]
[list]
[*]Survival Log 1.1.20151。游戏更新后模组可能暂时失效，直到模组更新为止。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/12]BepInEx for Survival Log[/url]，请先安装。[/*]
[*][url=https://www.nexusmods.com/survivallog/mods/61]Exiled's Toolkit[/url] 0.12.0 或更新版本。没有它本模组不会加载。打字时它让面板按键不起作用；面板关闭时，它把提示显示为弹出通知；它还让在 Mod Manager 的「模组」界面改动的设置无需重启即可生效。[/*]
[/list]

[size=5]安装[/size]
[list=1]
[*]安装 [url=https://www.nexusmods.com/survivallog/mods/12]BepInEx[/url] [i]（如果之前没有装过其他模组，先启动一次游戏，让 BepInEx 完成初始化，然后退出）[/i]。[/*]
[*]用同样的方式安装 Exiled's Toolkit，下载它的压缩包并解压到游戏目录。[/*]
[*]在「文件」页下载压缩包。[/*]
[*]把压缩包解压到游戏目录（[font=Courier New]SurvivalLog.exe[/font] 所在的文件夹）。压缩包里带有 BepInEx 文件夹，DLL 会自动放到正确位置。如果提示合并文件夹，选择合并。解压后应为：
[code]C:\Program Files (x86)\Steam\steamapps\common\Survival Log\BepInEx\plugins\PowerManager.dll[/code][/*]
[/list]
如果你在用 Game Rules 或 Machine Speed，请一并更新到 0.7.0 或更新版本：它们的电力设置已经移到这里。更新之前，Power Manager 会把这些设置留给旧版本处理，并提示你更新。

[size=5]卸载[/size]
从 [font=Courier New]BepInEx\plugins[/font] 文件夹中删除 [font=Courier New]PowerManager.dll[/font]。模组为节省电量而关闭的设备会保持关闭；可以在游戏自己的电力面板中重新打开。

[size=5]配置[/size]
自动加燃料、它的分钟数、负载削减的电量和燃料顺序可以在面板里修改。其他设置，例如按键以及电力和太阳能设置，可以在游戏内 Mod Manager 的「模组」界面修改（立即生效，蓄电池容量在下次启动游戏时生效），也可以在游戏关闭时编辑 [font=Courier New]BepInEx\config\com.dev.survivallog.powermanager.cfg[/font]。配置文件中每项设置都有中文说明，完整说明见 [url=https://github.com/AnExiledDev/SurvivalLog-PowerManager/blob/main/CONFIG.md]CONFIG.md[/url]（英文）。

[size=5]反馈[/size]
发现 bug 或有好点子？欢迎来 [url=https://discord.gg/trTsyxUFqg]Core Directive Discord[/url] 打个招呼，或在本页的「Posts」标签里发帖。报告 bug 时，请附上 [font=Courier New]BepInEx\LogOutput.log[/font] 和 [font=Courier New]BepInEx\power-manager.log[/font]。
想直接联系我，可以在 Discord 上给 [b]exiled.dev[/b] 发消息，我在 [url=https://discord.gg/MZGg4gQAUe]Survival Log 官方 Discord[/url] 里。也可以在 X 上找我：[url=https://x.com/AnExiledDev]@AnExiledDev[/url]。

[size=5]源代码与许可[/size]
基于 MIT 许可开源：[url=https://github.com/AnExiledDev/SurvivalLog-PowerManager]github.com/AnExiledDev/SurvivalLog-PowerManager[/url]
