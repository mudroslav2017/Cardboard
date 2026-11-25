# Ролики TikTok в Unity в проекте Cardboard

Эта инструкция помогает продолжить существующий ролик или поставить новый в той же фабричной сцене. Рабочий процесс идёт от правки C# и просмотра в Unity до покадрового экспорта и проверки MP4. Пример — «Настя и пчёлки», вертикальный ролик 1080 × 1920 при 30 кадрах/с.

## Что открыть в Unity

1. Открой папку `D:\Unity Projects\Cardboard` в Unity Hub. Версию редактора бери из `ProjectSettings/ProjectVersion.txt`. Для этого проекта использовалась `6000.6.2f1`. На этом компьютере редактор находится по пути `D:\Unity Projects\Unity\6000.6.2f1\Editor\Unity.exe`.
2. В окне **Project** зайди в **Assets → Scenes** и двойным щелчком открой **FactoryShift**. Файл сцены — `Assets/Scenes/FactoryShift.unity`. Меню экспорта проверяет путь активной сцены.
3. После правки кода дождись окончания компиляции Unity. Если команда **Cardboard** пропала или не запускается, открой **Window → General → Console** и проверь ошибки компиляции.
4. Для просмотра выбери **Cardboard → Preview Nastya and Bees**. Команда сама входит в Play Mode, если нужно. Вкладка **Game** показывает ролик в вертикальном кадре. С фокусом на вкладке **Game** нажми **Пробел**, чтобы поставить превью на паузу, **F7**, чтобы запустить с начала, или **Esc**, чтобы выйти из постановки.
5. Для экспорта выйди из превью и нажми активную кнопку **Play** на верхней панели, если редактор ещё в Play Mode. В режиме редактирования выбери **Cardboard → Export Nastya and Bees (MP4)**. Редактор сам запускает Play Mode и записывает ролик. Оставь его работать до завершения, затем проверь выходной файл.

## Где менять постановку

Пути в этом разделе указаны от корня проекта.

| Что менять | Где искать |
|---|---|
| Маршрут, прыжки, пчёлки, крупный план, зум и смена выражения | `Assets/Code/Scripts/Recording/FactoryBeesTrend.cs` |
| Подключение постановки к игре и закрытие по Esc | `Assets/Code/Scripts/CardboardFactoryGame.cs`, свойство `beesTrend` |
| Пауза игры, передача камеры постановке, восстановление сцены | `Assets/Code/Scripts/Recording/FactoryFilmSession.cs` |
| Меню **Cardboard**, формат, имя файла и запись MP4 | `Assets/Code/Editor/Recording/FactoryTrendExport.cs` |
| Модель Насти и материалы выражений | `Assets/Game_Assets/Resources/CardboardCharacters/HumanNastya.prefab` и соседние материалы |
| Исходная модель и текстура пчёлки | `Assets/Game_Assets/Tik Tok Trends/Bees/bee.fbx` и `M_bee_base color.png` в той же папке |
| Подготовленная пчёлка для постановки | `Assets/Game_Assets/Tik Tok Trends/Bees/Resources/BeesTrend/Bee.prefab` |

### Как устроены кадры

`FactoryBeesTrend` создаёт персонажей, свет и объём постобработки при запуске постановки. Он использует `FactoryFilmSession`, чтобы временно остановить игровую логику и взять под управление штатную камеру фабрики. После выхода временные объекты удаляются, а игра возвращает свои настройки.

Для каждого кадра экспортёр вызывает `Seek(номерКадра / 30f)`. Поэтому вычисляй положение, вращение, зум и выражение по переданному времени. Для инерционной камеры текущий код заранее строит таблицу поз с шагом 1/120 секунды, а при записи интерполирует между соседними позами. Такой же `Seek` показывает нужный кадр при перемотке.

- **Движение Насти.** `PoseHops` двигает и наклоняет персонажа целиком. `HopPosition` задаёт путь, `HopHeight` задаёт высоту прыжка. Исходная сетка используется без деформации. При наклоне положение корректируется по опорному краю у ног.
- **Рой и инерционная камера.** `PoseSwarm` и `SwarmPosition` задают орбиты и подъёмы. Сглаженное направление берётся по позициям немного до и после текущего времени. `BuildTrackingShot` и `PoseTrackingShot` отвечают за запаздывание и догоняющее движение камеры.
- **Финальный план.** `PoseCloseup` отвечает за правый ракурс, реакцию Насти, зум и фокус. `PoseLeadBee` задаёт подъём спиной к камере, разворот в профиль, подлёт и отскок. Для крупного плана точка касания совмещается с носом в проекции камеры, чтобы плоская модель лица и широкие крылья визуально стыковались. При смене стороны камеры проверь и подлёт, и контакт с нового ракурса.
- **Выражение и резкость.** На `ContactFrame` вызывается `SetState(FactoryFaceState.Suspicious)`. После касания `PoseCloseup` сужает поле зрения, на короткое время уводит плоскость фокуса к камере, затем возвращает её на лицо. В конце ролика `turnToCamera` задаёт небольшой поворот Насти.

### Как считать хронометраж

Номера кадров начинаются с нуля. Время события в секундах равно номеру кадра, делённому на `FrameRate`. Длительность файла равна `FrameCount / FrameRate`.

| Событие в текущей версии | Время |
|---|---:|
| Склейка на крупный план, кадр 189 | 6,30 с |
| Пчёлка начинает разворот в профиль | 8,07 с |
| Касание и смена выражения, кадр 306 | 10,20 с |
| Максимальный расфокус во время зума | около 10,99 с |
| Фокус возвращается на лицо | 11,70 с |
| Начало двухсекундного продления, кадр 375 | 12,50 с |
| Конец ролика, 435 кадров | 14,50 с |

При 30 кадрах/с ровно две секунды — 60 кадров. Если добавляешь действие в хвост, увеличь `FrameCount` на 60 и поставь начало нового действия на границе прежней длительности. При изменении момента склейки согласуй `CloseFrame` и параметры прогрева в записи формата. При переносе касания согласуй `ContactFrame` с подходом пчёлки и реакцией персонажа.

### Как подготовить новую модель

1. Сложи FBX и текстуры в отдельную папку внутри `Assets/Game_Assets/Tik Tok Trends`. После импорта выбери модель в окне **Project** и проверь её геометрию и материалы в **Inspector**.
2. Создай материал через **Assets → Create → Material**, выбери шейдер **Universal Render Pipeline/Lit** и назначь исходную цветовую текстуру в **Base Map**. В сцене назначь материал компоненту **Mesh Renderer** нужного объекта.
3. Сделай корневой объект префаба с удобной точкой вращения. Для летящего объекта направь его видимую переднюю сторону вдоль локальной оси **+Z**, если движение использует `Quaternion.LookRotation`. Создай префаб, перетащив его корень из **Hierarchy** в нужную папку **Project**. Для загрузки через `Resources.Load` используй папку `Resources`, как у `BeesTrend/Bee.prefab`.
4. При работе через код тот же префаб можно собрать временным Editor-скриптом. `AssetDatabase.LoadAssetAtPath` загружает FBX и текстуру, а `PrefabUtility.SaveAsPrefabAsset` сохраняет объект с настроенными материалом, масштабом и ориентацией.

### Как подключить новый ролик

1. Возьми `FactoryBeesTrend` или другой подходящий класс из `Assets/Code/Scripts/Recording` за пример. Новый класс реализует `IFactoryTrendRecording` из `FactoryFilmSession.cs`. Его точки входа — `Begin(bool preview)`, `Seek(float seconds)`, `Stop(bool restoreMenu = true)` и свойства `Active`, `Lens`, `Soundtrack`.
2. Подключи компонент в `CardboardFactoryGame` так же, как `beesTrend`. Вызови остановку при сбросе смены и выходе по Esc. В самом классе постановки используй `OnDisable` для очистки. Используй существующие модели и материалы из `Resources`, когда ролик должен брать их через `Resources.Load`.
3. В `FactoryTrendExport` добавь значение `Kind`, соответствующую запись `Format` и команды `MenuItem` для превью и экспорта. Порядок записей `Formats` соответствует значениям `Kind`. Для ролика с музыкой передай `AudioClip` через `Soundtrack`. «Настя и пчёлки» возвращает `null`, поэтому MP4 не содержит аудиодорожку.

## Как появляется MP4

`FactoryTrendExport` рендерит камеру в `RenderTexture` размером 1080 × 1920 и передаёт кадры в `UnityEditor.Media.MediaEncoder`. Для «Насти и пчёлок» задан H.264 High с целевым битрейтом 20 Мбит/с. Четыре прогревочных рендера в начале и на склейке подготавливают материалы и видимые объекты сцены. Они не входят в `FrameCount`.

Файл появляется в `Recordings/TikTok`. Имя берётся из записи `Format`. При совпадении имени экспортёр добавляет дату и время. В `Library/BeesTrendRender.txt` экспортёр записывает состояние `Waiting for Play Mode`, затем `Rendering 435 frames` и в конце `DONE` с абсолютным путём или `ERROR` с описанием ошибки. Для другой версии число в статусе соответствует её `FrameCount`.

## Пакетная запись в отдельной копии

Этот способ использовался для последних версий, пока открытый редактор мог содержать несохранённую сцену. Запусти установленный Unity с графикой и отдельным `-projectPath`. Источники правятся в основном проекте, а копия нужна для запуска, проверки и записи.

### Создай копию проекта

В PowerShell выполни следующий блок. Если проект или Unity находятся в другом месте, подставь фактические пути. Блок создаёт отдельную папку под `Temp`.

```powershell
$projectDir = 'D:\Unity Projects\Cardboard'
$unityExe = 'D:\Unity Projects\Unity\6000.6.2f1\Editor\Unity.exe'
$batchName = 'BeesBatch-' + [Guid]::NewGuid().ToString('N')
$renderProject = Join-Path $projectDir ('Temp\' + $batchName)
$renderLibrary = Join-Path $renderProject 'Library'

New-Item -ItemType Directory -Path $renderLibrary -Force | Out-Null
foreach ($folder in @('Assets', 'Packages', 'ProjectSettings')) {
    Copy-Item -LiteralPath (Join-Path $projectDir $folder) -Destination $renderProject -Recurse
}

$localPackages = Join-Path $projectDir 'Library\PackageCache'
if (Test-Path -LiteralPath $localPackages) {
    Copy-Item -LiteralPath $localPackages -Destination $renderLibrary -Recurse
}
```

Каждый запуск использует собственные отчёт и выходную папку. При первом старте копии Unity компилирует C# и импортирует ассеты. Следующие запуски той же копии используют уже подготовленную `Library`. Если в основном проекте внесена новая правка, скопируй изменённые файлы в рабочую копию перед повторным рендером.

### Добавь временную точку запуска

В копии, путь к которой хранится в `$renderProject`, создай `Assets/Code/Editor/Recording/BeesBatchRender.cs` со следующим содержимым. Помощник открывает `FactoryShift`, дожидается создания игрока и камеры в Play Mode, вызывает штатный экспорт и завершает отдельный процесс по результату записи.

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cardboard.Editor
{
    [InitializeOnLoad]
    internal static class BeesBatchRender
    {
        const string State = "Cardboard.BeesBatchRender.State";
        const string Report = "Library/BeesTrendRender.txt";

        static BeesBatchRender() => EditorApplication.update += Tick;

        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/FactoryShift.unity");
            SessionState.SetInt(State, 1);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            int state = SessionState.GetInt(State, 0);
            if (state == 0 || EditorApplication.isCompiling) return;

            if (state == 1)
            {
                if (!EditorApplication.isPlaying || !CardboardFactoryGame.Instance
                    || !CardboardRunner.Player || !Camera.main) return;
                FactoryTrendExport.ExportBees();
                SessionState.SetInt(State, 2);
                return;
            }

            if (!File.Exists(Report)) return;
            string result = File.ReadAllText(Report);
            bool done = result.StartsWith("DONE ");
            if (!done && !result.StartsWith("ERROR ")) return;

            SessionState.EraseInt(State);
            Debug.Log(result);
            EditorApplication.Exit(done ? 0 : 1);
        }
    }
}
```

### Запусти Unity с графикой

В той же сессии PowerShell выполни следующий блок. Оставь процесс работать до завершения экспорта. `-force-d3d11` даёт Unity графическое устройство для записи кадров.

```powershell
$renderLog = Join-Path $renderProject 'BeesBatchUnity.log'
$unityArgs = @(
    '-batchmode'
    '-force-d3d11'
    '-projectPath'
    ('"{0}"' -f $renderProject)
    '-buildTarget'
    'StandaloneWindows64'
    '-logFile'
    ('"{0}"' -f $renderLog)
    '-executeMethod'
    'Cardboard.Editor.BeesBatchRender.Run'
)

$renderProcess = Start-Process -FilePath $unityExe -ArgumentList $unityArgs -WindowStyle Hidden -PassThru
$renderProcess.Id
```

Следи за `$renderLog` во время импорта и за `Library/BeesTrendRender.txt` внутри копии после старта экспорта. Если отчёт ещё не появился, проверь, дошёл ли Unity до Play Mode и нет ли ошибок компиляции. При `ERROR` сначала прочитай текст ошибки и последние строки лога. Если лог по заданному пути отсутствует, проверь `Logs/Editor.log` внутри копии, наличие нового MP4 и время записи отчёта.

После `DONE` проверь именно файл из отчёта. Затем перенеси или скопируй проверенный MP4 из копии в `D:\Unity Projects\Cardboard\Recordings\TikTok` под свободным именем. Временная точка запуска остаётся в копии, в проекте для дальнейшей работы нужны исходные изменения и готовый ролик.

## Как проверить готовый файл

1. Проверь, что Unity сообщил `DONE`, а указанный в отчёте файл открывается. Длительность пересчитай из `FrameCount` и `FrameRate`.
2. Проверь метаданные и декодирование всего MP4 через FFprobe и FFmpeg, если они доступны. На этом компьютере оба исполняемых файла находятся в `D:\AI\Deep Cam\ffmpeg`.
3. Посмотри кадры из самого MP4. Сверь первый кадр, первый кадр после склейки, кадр касания, пик расфокуса, кадр с восстановленной резкостью и последний кадр. Для правки движения посмотри несколько соседних кадров или проиграй нужный отрезок целиком.
4. Отдай пользователю ссылку на фактический MP4 в `Recordings/TikTok`. В ответе укажи, что изменилось, и длительность, если её просили изменить.

```powershell
$mp4 = 'D:\Unity Projects\Cardboard\Recordings\TikTok\Nastya and Bees - Final Look 9x16.mp4'
$ffprobe = 'D:\AI\Deep Cam\ffmpeg\ffprobe.exe'
$ffmpeg = 'D:\AI\Deep Cam\ffmpeg\ffmpeg.exe'

& $ffprobe -v error -select_streams v:0 -count_frames -show_entries 'stream=codec_name,width,height,r_frame_rate,nb_read_frames:format=duration' -of json $mp4
& $ffmpeg -v error -xerror -i $mp4 -map 0:v:0 -f null NUL
$LASTEXITCODE
```

Для проверки ключевых кадров можно извлечь их в PNG. Здесь указаны номера кадров текущей версии. Первый PNG соответствует кадру 0, следующие идут по возрастанию номера кадра.

```powershell
$projectDir = 'D:\Unity Projects\Cardboard'
$framesDir = Join-Path $projectDir ('Temp\BeesFrames-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $framesDir -Force | Out-Null
$filter = "select='eq(n,0)+eq(n,189)+eq(n,305)+eq(n,306)+eq(n,330)+eq(n,351)+eq(n,434)',scale=360:640"
& $ffmpeg -v error -i $mp4 -vf $filter -fps_mode vfr (Join-Path $framesDir 'frame-%02d.png')
```

Для текущей версии результат — `h264`, `1080 × 1920`, `30/1`, `435` декодированных кадров, `14.500000` секунды. Код выхода `0` после команды FFmpeg означает, что она завершила декодирование без ошибки.

## Текст для нового чата с GPT

Скопируй этот текст и добавь конкретную правку. Если GPT работает с той же папкой проекта, он сможет прочитать инструкцию и открыть указанные исходники.

> Работаем с Unity-проектом `D:\Unity Projects\Cardboard`. Сначала прочитай `VIDEO_WORKFLOW.md` в корне проекта и проверь актуальный код. Для ролика «Настя и пчёлки» используй сцену `FactoryShift`, постановку `FactoryBeesTrend.cs` и штатный `FactoryTrendExport`. Внеси мою правку в исходники, запиши MP4, проверь длительность и ключевые кадры в готовом видео. Сохрани файл в `Recordings/TikTok` и дай ссылку на него. Моя правка указана ниже.
