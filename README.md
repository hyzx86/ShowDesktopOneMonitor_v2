# ShowDesktopOneMonitor

## V2 版本增强
- 增加开机启动
- 管理员权限改为可选
- 增加覆盖 Win+D 快捷键覆盖可切换
<img width="284" height="163" alt="image" src="https://github.com/user-attachments/assets/16a04666-39fb-4cc5-92dc-5c6c9b4f0d33" />


## Description
Adds abillity to Show Desktop (Win + D) only for One Monitor!
Works exactly like Win + D does **plus additional features**:
- Shows desktop (minimizes all windows)
- Restores windows to their previous states
- Minimizes all windows if user changed state of any window or one of the windows opened / closed (exactly how Win + D works)  
### Plus: ###
- Minimizes/Restores only windows on specific monitor, remembering their states

## Installation
1. Download and extract archive somewhere  
*See Releases section: https://github.com/ruzrobert/ShowDesktopOneMonitor/releases*
2. Create task in Task Scheduler:  
- Specify path to *ShowDesktopOneMonitor.exe*
- Trigger: *Run only when user is logged on*
- Check *Run with highest priveleges*
- On *Settings* tab make sure that task will not be stopped after running longer than some days, for example.  
Note: program has icon in tray, but unfortunatelly it is invisible, if app is started from Task Scheduler :(

## Usage
Press key combination: *Left Windows Key + Left Shift + D* to minimize/restore windows **on monitor where cursor is currently on**.

## Credits
In core of the project is @FrigoCoder's code: https://github.com/FrigoCoder/FrigoTab  
His code allows to get list of windows exactly like Alt + Tab does, what was excellent for my task.

## License
Copyright (c) 2019 Robert Ruzin. Licensed under the GPL-3.0 license.
