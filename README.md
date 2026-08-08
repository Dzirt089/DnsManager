[🇷🇺 На русском](#русский) | [🇬🇧 In English](#english)

---

<a id="english"></a>
# DNS Manager

Desktop application for Windows 11 (WPF, .NET 10, C#). It detects the active network adapter (Wi-Fi / Ethernet, etc.) and switches its DNS between "Automatic (DHCP)" mode and a manual profile:

- **Preferred DNS (IPv4):** `111.88.96.50` — DNS over HTTPS is **enabled** (automatic template), fallback to plaintext is **disabled**.
- **Alternate DNS (IPv4):** `111.88.96.51` — standard DNS, DoH is **disabled**.

## Features

- "Enable DNS" / "Disable (DHCP)" buttons for the selected network adapter
- Network type detection (Wi-Fi, Ethernet, etc.) and adapter selection
- DNS profile presets (JSON, `%LOCALAPPDATA%\DnsManager\presets.json`)
- Domain resolving test
- DNS servers benchmark (latency, packet loss)
- System tray + autostart on Windows login
- Log of all system actions and responses (UI panel + `%LOCALAPPDATA%\DnsManager\logs\` file)

## Running the Application

The application requires administrator privileges (UAC): modifying Windows DNS settings is only possible as an administrator.

```bash
dotnet build -c Release
dotnet run --project src/DnsManager.App
```

## Testing

```bash
dotnet test
```

## Structure

- `src/DnsManager.Core` — models, PowerShell commands, services (no WPF)
- `src/DnsManager.App` — WPF application (MVVM)
- `tests/DnsManager.Tests` — unit tests

## Installation

Download the latest release from the [Releases](#) page and run `Setup.exe`.

## License

This project is open-source and distributed under the MIT License.

Code signing is provided for free by the [SignPath Foundation](https://about.signpath.io/product/open-source).

---

<a id="русский"></a>
# DNS Manager

Десктоп-приложение для Windows 11 (WPF, .NET 10, C#). Определяет активный сетевой адаптер (Wi-Fi / Ethernet и др.) и переключает его DNS между режимом «Автоматически (DHCP)» и ручным профилем:

- **Предпочтительный DNS (IPv4):** `111.88.96.50` — DNS over HTTPS **включено** (автоматический шаблон), возврат к обычному тексту **отключён**.
- **Дополнительный DNS (IPv4):** `111.88.96.51` — обычный DNS, DoH **выключен**.

## Возможности

- Кнопки «Включить DNS» / «Выключить (DHCP)» для выбранного сетевого адаптера
- Детект типа сети (Wi-Fi, Ethernet и др.) и выбор адаптера
- Пресеты DNS-профилей (JSON, `%LOCALAPPDATA%\DnsManager\presets.json`)
- Тест резолвинга доменов
- Бенчмарк DNS-серверов (латентность, потери)
- Системный трей + автозапуск при входе в Windows
- Лог всех действий и ответов системы (панель + файл `%LOCALAPPDATA%\DnsManager\logs\`)

## Запуск

Приложение требует прав администратора (UAC): изменение DNS-настроек Windows возможно только из-под админа.

```bash
dotnet build -c Release
dotnet run --project src/DnsManager.App
```

## Тесты

```bash
dotnet test
```

## Структура

- `src/DnsManager.Core` — модели, PowerShell-команды, сервисы (без WPF)
- `src/DnsManager.App` — WPF-приложение (MVVM)
- `tests/DnsManager.Tests` — юнит-тесты

## Установка

Скачайте последний релиз со страницы [Releases](#) и запустите `Setup.exe`.

## Лицензия

Этот проект имеет открытый исходный код и распространяется под лицензией MIT.

Code signing is provided for free by the [SignPath Foundation](https://about.signpath.io/product/open-source).