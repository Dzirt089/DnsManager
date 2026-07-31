# Настройка CI/CD для DNS Manager (GitHub Actions + Velopack)

Пошаговая инструкция по настройке бесплатного CI/CD для проекта DNS Manager
(WPF, .NET 10): автоматическая сборка, тесты, создание установщика `Setup.exe`
и доставка обновлений через GitHub Releases.

> **Проверка актуальности:** 31.07.2026. Проверено через официальные источники:
> SDK .NET 10.0.302 (LTS до 14.11.2028), GitHub Actions (лимиты minutes),
> GitHub Pages (политика free-плана), NuGet.org (доступность), Velopack 1.2.0,
> Inno Setup 6.7. Перед выполнением шагов сверьте версии в официальных доках —
> инструменты развиваются быстро.

---

## 1. Что вы получите

Схема пайплайна (всё бесплатно, 0 ₽/мес):

```
git push / PR ──► CI: build + test ──► publish ──► vpk pack (Setup.exe + пакеты)
                                                     │
git tag v1.0.0 ──► Release job ──────────────────────┴─► GitHub Release (Releases)
                                                           │
Пользователь: Setup.exe с GitHub  или  авто-обновление из приложения
```

- **CI** — сборка и тесты на каждый push и pull request.
- **Release** — по git-тегу `v1.0.0` собирается релиз: установщик `Setup.exe`
  и пакеты обновлений, всё загружается в GitHub Release.
- **Обновления** — приложение само проверяет новую версию и обновляется
  (Velopack, NuGet-пакет, бесплатный, open source).

Стоимость на **публичном** репозитории: **0 ₽**. Лимиты см. раздел 9.

---

## 2. Требования

| Что | Требование |
|---|---|
| Аккаунт GitHub | Бесплатный (Free). Проверьте его статус для РФ — см. раздел 8 |
| Репозиторий | Публичный (исходники видит любой; зато Actions без лимита минут и Releases бесплатны) |
| Git на машине | `git --version` — установлен |
| Локальный SDK | .NET SDK 10.x (в проекте уже `net10.0-windows`) |

### 2.1. Зафиксировать версию SDK (global.json)

На раннерах GitHub может стоять другой SDK. Чтобы сборка была воспроизводимой,
зафиксируйте версию в корне репозитория `global.json`:

```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestFeature"
  }
}
```

`rollForward: latestFeature` — разрешит более свежие патчи 10.0.x, но не
перепрыгнет на другой мажор.

### 2.2. Убедиться, что проект собирается локально

```bash
dotnet build -c Release
dotnet test
```

---

## 3. Шаг 1. CI на каждый push и PR

Файл `.github/workflows/ci.yml`:

```yaml
name: CI

on:
  push:
    branches: [ main ]
  pull_request:

permissions:
  contents: read

jobs:
  build:
    runs-on: windows-latest          # WPF собирается только на Windows
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'   # берётся из global.json, если он есть

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build -c Release --no-restore

      - name: Test
        run: dotnet test --no-build -c Release

      - name: Upload publish artifact (опционально, для отладки)
        uses: actions/upload-artifact@v4
        with:
          name: dnsmanager-build
          path: src/DnsManager.App/bin/Release/net10.0-windows/
          if-no-files-found: error
```

Пояснения:
- `runs-on: windows-latest` — обязателен: WPF-приложение (и Velopack-паковка)
  собираются только на Windows.
- `dotnet-version: '10.0.x'` + `global.json` — одинаковая версия SDK везде.
- Тесты падают → PR не зелёный. Это ваш «забор» от поломок.

---

## 4. Шаг 2. Релизный пайплайн (установщик + GitHub Release)

Файл `.github/workflows/release.yml`. Запускается по тегу `v*`:

```yaml
name: Release

on:
  push:
    tags: [ 'v*' ]

permissions:
  contents: write              # нужно, чтобы gh release create мог загрузить файлы

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Build and test
        run: |
          dotnet build -c Release
          dotnet test --no-build -c Release

      - name: Publish (self-contained, win-x64)
        shell: pwsh
        run: |
          $version = $env:GITHUB_REF_NAME.TrimStart('v')
          dotnet publish src/DnsManager.App -c Release `
            -r win-x64 --self-contained true `
            -p:PublishSingleFile=false `
            -p:Version=$version `
            -o publish

      - name: Install Velopack CLI
        run: dotnet tool install --global vpk

      - name: Pack with vpk
        shell: pwsh
        run: |
          $version = $env:GITHUB_REF_NAME.TrimStart('v')
          vpk pack `
            --packId "DnsManager.App" `
            --packVersion $version `
            --packDir publish `
            --mainExe "DnsManager.App.exe" `
            --outputDir releases `
            --icon "src/DnsManager.App/Assets/app.ico"

      - name: Publish GitHub Release
        env:
          GH_TOKEN: ${{ github.token }}
        shell: pwsh
        run: |
          gh release create "${{ github.ref_name }}" releases/* --generate-notes
```

Что происходит:
1. Сборка + тесты (релиз не выйдет, если тесты красные).
2. `dotnet publish` — self-contained `win-x64` без single-file (Velopack нужно
   «нормальное» приложение из файлов, single-file не поддерживается).
   `-p:Version=$version` — версия exe совпадает с тегом (`v1.0.0` → `1.0.0`).
3. `vpk pack` создаёт в папке `releases`:
   - `Setup.exe` — установщик для пользователей;
   - `RELEASES` — манифест обновлений;
   - `DnsManager.App-1.0.0-full.nupkg` (+ дельта-пакеты для будущих версий).
4. `gh release create ... releases/*` — загружает все три файла в GitHub Release
   с авто-описанием.

> **Про runtime .NET:** self-contained включает .NET Desktop Runtime в установщик
> (~150 МБ). Пользователю не нужно ставить .NET отдельно. Если хотите меньший
> размер — уберите `-r win-x64 --self-contained true` и добавьте бутстраппер
> .NET Desktop Runtime (как в вашем ClickOnce-профиле), но тогда у пользователя
> должен быть установлен .NET.

---

## 5. Шаг 3. Авто-обновления в приложении (Velopack)

### 5.1. Подключить пакет

```bash
dotnet add src/DnsManager.App package Velopack --version 1.2.0
```

### 5.2. Инициализация при старте

В `src/DnsManager.App/App.xaml.cs`, в `OnStartup` **первой строкой**:

```csharp
protected override void OnStartup(StartupEventArgs e)
{
    VelopackApp.Build().Run();   // первым — обрабатывает обновления/первый запуск
    base.OnStartup(e);
    // ... остальной код (логирование, окно и т.д.)
}
```

### 5.3. Кнопка «Проверить обновления»

URL источника — папка файлов последнего релиза:

```csharp
private const string UpdateFeedUrl =
    "https://github.com/<OWNER>/<REPO>/releases/latest/download";

[RelayCommand]
private async Task CheckForUpdatesAsync()
{
    try
    {
        using var mgr = new UpdateManager(UpdateFeedUrl);
        var update = await mgr.CheckForUpdatesAsync();
        if (update is null)
        {
            StatusBarText = "Установлена актуальная версия.";
            return;
        }

        StatusBarText = "Скачивание обновления...";
        await mgr.DownloadUpdatesAsync(update);
        mgr.ApplyUpdatesAndRestart();   // закрывает приложение и ставит обновление
    }
    catch (Exception ex)
    {
        _log.Error("update.check", $"Не удалось проверить обновления: {ex.Message}", ex);
        StatusBarText = "Ошибка проверки обновлений";
    }
}
```

Подсказки:
- Команду привяжите к кнопке в табе «Настройки» или к пункту меню трея
  (`CheckForUpdatesCommand`).
- `UpdateManager` и `UpdateManagerAsync` — потокобезопасны; приложение
  однократное — `using` достаточно.
- Приложение с `requireAdministrator` (в `app.manifest`) обновляется корректно:
  Velopack перезапускает установку с повышенными правами.

---

## 6. Шаг 4. Создание релиза и проверка

```bash
# 1. Все изменения в main
git add -A && git commit -m "chore: release 1.0.0"

# 2. Тег → триггер release.yml
git tag v1.0.0
git push origin main --tags
```

1. Откройте **Actions** → вкладка `Release` — пайплайн идёт ~5–10 минут.
2. По завершении откройте **Releases** → `v1.0.0` → там `Setup.exe`,
   `RELEASES` и `*.nupkg`.
3. Скачайте и установите `Setup.exe` на тестовую машину.
4. Поднимите версию: `git tag v1.0.1 && git push origin v1.0.1` → новый релиз.
5. В установленном приложении нажмите «Проверить обновления» — приложение
   скачает и применит 1.0.1 автоматически.

---

## 7. Санкционные ограничения для РФ и бесплатные альтернативы

GitHub (Microsoft) ограничивает **платные** сервисы для пользователей и
организаций из санкционных юрисдикций (включая РФ). Что это значит на практике:

| Функция | Статус для РФ (free-аккаунт) |
|---|---|
| Публичный репозиторий, Issues, PR, Wiki | Обычно доступны |
| GitHub Actions (публичный репо) | Бесплатно, без лимита минут — обычно работает |
| GitHub Releases (раздача файлов) | Бесплатно — обычно работает |
| GitHub Pages (публичный репо) | Бесплатно — обычно работает |
| Платные планы, Copilot, Codespaces, Advanced Security | Ограничены/недоступны для покупки |
| Приватные репозитории + Actions | 2000 мин/мес бесплатно, но аккаунт из РФ может быть ограничен при оплате Pro |

**Перед началом:** проверьте статус своего аккаунта (Settings → Billing and
plans; попробуйте запустить Actions). Точная политика меняется и зависит от
аккаунта, поэтому проверяйте на момент настройки.

**Если облачные раннеры недоступны или нестабильны:**
- **Self-hosted runner** (своя Windows-машина): подключается к репозиторию,
  минуты не расходуются вообще, работает из РФ:
  - Settings → Actions → Runners → New self-hosted runner;
  - на машине: `./config.cmd --url https://github.com/<OWNER>/<REPO> --token <TOKEN>`;
  - в workflow: `runs-on: [self-hosted, windows]`.
- **Если GitHub медленный из РФ** (скорость не гарантирована): файлы релизов
  можно дополнительно раздавать через любое удобное хранилище (свой сервер,
  S3-совместимое и т.п.), но это уже за рамками «бесплатно из коробки».

---

## 8. Лимиты и цены (публичный репозиторий, GitHub Free)

| Ресурс | Лимит | Комментарий |
|---|---|---|
| GitHub Actions | Безлимит минут (public) | 2000 мин/мес — только для приватных |
| Параллельные jobs | 20 | Практически не упрётесь |
| Размер файла в Release | до 2 ГБ | Установщику нужно ~150 МБ — с запасом |
| Трафик GitHub Releases | Не лимитирован | Раздача бесплатна |
| Storage артефактов | 500 МБ (public: зависит от плана) | Артефакты CI живут 90 дней |
| Хранение в репозитории | неограничено (мягкий лимит) | — |
| GitHub Pages | 1 ГБ сайт, файл до 100 МБ | Только для публичных репо на Free |

Потребление для этого проекта: ~1 job на push/PR (~1–2 мин) + 1 job на релиз
(~5–10 мин). Даже на приватном репозитории хватило бы 2000 минут с запасом.

---

## 9. Troubleshooting

| Симптом | Причина / решение |
|---|---|
| `MSB3027: ... DnsManager.App.exe (PID) блокирует файл` | Приложение запущено — закройте его перед `dotnet build`; на CI такого нет |
| SmartScreen: «Windows защитил ваш компьютер» | Установщик не подписан. Это нормально для бесплатных решений: «Подробнее → Выполнить в любом случае». Решение — купить code-signing сертификат (платно, недоступен из РФ у части вендоров — проверяйте) |
| `vpk: command not found` | `dotnet tool install --global vpk`, затем перезапустите shell или добавьте `%USERPROFILE%\.dotnet\tools` в PATH |
| `gh: Permission denied` | В job не хватает прав: `permissions: contents: write` |
| Релиз создан, но файлы не загрузились | `gh release create ... releases/*` — проверьте, что `releases/` не пуста и что вы в папке репозитория |
| Обновление «не видит» новую версию | URL источника должен заканчиваться на `releases/latest/download` (без имени файла); `--packVersion` должен быть выше установленной версии |
| Версия приложения не совпадает с тегом | Используйте `-p:Version=$version` в `dotnet publish` (см. Шаг 2) |
| `dotnet restore` падает с NuGet | Проверьте доступ к `api.nuget.org` (в РФ обычно доступен). Для частных корпоративных прокси — `NUGET_PACKAGES`/nuget.config |
| ClickOnce-профиль проекта (`ClickOnceProfile.pubxml`) | Он остаётся в репо, но в Velopack-пайплайне не используется. Не мешает |

---

## 10. Итог

После выполнения шагов у вас:

- ✅ `.github/workflows/ci.yml` — сборка и тесты на каждый push/PR;
- ✅ `.github/workflows/release.yml` — установщик и релиз по тегу `v*`;
- ✅ авто-обновления Velopack в приложении;
- ✅ дистрибуция через GitHub Releases — бесплатно, с учётом ограничений для РФ.

**Рекомендация:** перед настройкой ещё раз сверьте версии инструментов
(см. раздел «Проверка актуальности» в начале) — .NET SDK, `setup-dotnet@v4`,
`vpk`, Velopack NuGet. Ссылки только на официальные источники:
[docs.github.com/actions](https://docs.github.com/en/actions),
[velopack.io](https://velopack.io), [nuget.org](https://www.nuget.org),
[learn.microsoft.com/dotnet](https://learn.microsoft.com/dotnet).
