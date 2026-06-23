# PixCake Helper (像素蛋糕多账号管理助手)

一个为修图软件「像素蛋糕」量身定制的第三方多账号与预设管理工具。
使用纯 C# WinForms 编写，支持 Windows 10/11，单文件 `.exe` 无需安装环境即可运行。

## ✨ 核心功能

- 🚀 **自动化登录**：一键切换和登录多个像素蛋糕账号，免去繁琐的手动输入。
- 📦 **预设代码管理**：支持保存、删除、和一键唤醒像素蛋糕导入预设（支持从分享链接智能提取口令）。
- 📥 **批量账号导入**：支持从剪贴板一键批量导入账号与密码。
- 🎨 **现代 UI 设计**：内置深色模式，圆角与阴影边缘，完美支持 Windows 多显示器高 DPI 缩放。
- 🔒 **纯本地存储**：数据仅以 JSON 格式存储在本地 `accounts.json` 中，安全透明。

## 📦 如何使用

### 方式一：直接运行 (推荐)
直接在 [Releases](https://github.com/Elosiad/PixCakeHelper/releases/latest) 页面下载最新版本的 `PixCakeHelper.exe`。
将它放在任意文件夹中运行即可。第一次运行会自动生成配置文件。



软件会在同级目录下生成 `accounts.json`。你可以随时在此处备份或修改你的账号和密码：

```json
{
  "password": "你的全局密码(如果所有账号共用)",
  "accounts": [
    {
      "username": "13800138000",
      "password": "可独立配置密码(留空则用全局密码)",
      "used": false
    }
  ],
  "presets": []
}
```

## 🛠 技术细节

* 语言: C# 5.0 (兼容系统自带的 csc.exe，零依赖)
* 框架: Windows Forms (Win32 P/Invoke 优化)
* UI特性: 动态 DPI 感知、Owner-drawn ListBox、无边框阴影、原生控件暗色主题映射 (`DarkMode_Explorer`)。

## 📜 许可证

MIT License
