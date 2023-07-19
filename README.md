# Tabs

[![LICENSE](https://img.shields.io/badge/MIT-tabs-39CEBF?style=for-the-badge)](https://github.com/sduo/tabs)
[![LICENSE](https://img.shields.io/badge/MIT-tabs--web-39CEBF?style=for-the-badge)](https://github.com/sduo/tabs-web)
[![LICENSE](https://img.shields.io/badge/MIT-tabs--extension-39CEBF?style=for-the-badge)](https://github.com/sduo/tabs-extension)

[![.Net 6](https://img.shields.io/badge/.Net%206-9C72D2?style=for-the-badge)](https://dotnet.microsoft.com/)
[![PNPM](https://img.shields.io/badge/PNPM-F69220?style=for-the-badge)](https://pnpm.io/)
[![UMI](https://img.shields.io/badge/UMI-1890FF?style=for-the-badge)](https://github.com/sduo/tabs/blob/main/LICENSE)
[![ANTD](https://img.shields.io/badge/ANTD-1677FF?style=for-the-badge)](https://ant.design/)
[![PLASMO](https://img.shields.io/badge/PLASMO-000000?style=for-the-badge)](https://github.com/PlasmoHQ/plasmo)

# 浏览器扩展

[![Chrome](https://img.shields.io/badge/Chrome-4FA13A?style=for-the-badge)](https://chrome.google.com/webstore/detail/pbjicciopkilinmadklbdcniabodgjcg)
[![Edge](https://img.shields.io/badge/Edge-0C59A4?style=for-the-badge)](https://www.microsoft.com/store/productId/0RDCKC8R502R)
[![Firefox](https://img.shields.io/badge/Firefox-EC8840?style=for-the-badge)](https://addons.mozilla.org/zh-CN/firefox/addon/tabs-extension/)

# 说明

Tabs 为浏览器标签页提供暂存服务，能够快速的将当前标签页或所有标签页保存到指定的服务器上。以便以后在任何时间和设备上再次访问。

# 数据保护

* Tabs 采用可前后端分离的形式进行部署，。
* Tabs 采用 HMAC—SHA256 的令牌方式对接口进行保护，可自行在配置文件中指定 ```salt``` 值生成特有的令牌认证密钥。
* Tabs 支持多用户同时使用，不同用户的数据无法相互访问。

# 隐私保护

* Tabs 采用自建的方式提供服务，所有数据掌控在自己手中。
* Tabs 不会与除指定服务器以外地方的进行通讯。
