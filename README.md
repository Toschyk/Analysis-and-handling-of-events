# Analysis and handling of events

# Практическая работа: Анализ и обеспечение обработки исключительных ситуаций

## 1. Создаем проект и подключаем библиотеки

Создаем стандартное консольное приложение в Visual Studio. 

Первым делом нам нужно подключить пространство имен `System.Globalization`. Оно нам понадобится, чтобы программа строго понимала, что мы вводим (например, чтобы точка и запятая не путали парсер чисел). 

Сразу же объявим класс `Program` и заведем в нем две переменные для путей к файлам. По умолчанию мы будем сохранять файлы рядом с нашим исполняемым файлом (`AppContext.BaseDirectory` — это удобный способ узнать папку, откуда запущена программа). Там же заведем флажок `SimulateUnexpected`, который поможет нам искусственно вызвать «непредвиденную» ошибку для теста.

```csharp
using System.Globalization;

internal static class Program
{
    // Пути по умолчанию (рядом с .exe файлом)
    private static string ResultPath = Path.Combine(AppContext.BaseDirectory, "result.txt");
    private static string LogPath = Path.Combine(AppContext.BaseDirectory, "app.log");

    // Флаг для тестирования непредвиденной ошибки
    private static bool SimulateUnexpected;
```

## 2. Пишем каркас: аргументы командной строки и точка входа

Представьте, что нам нужно проверить, как программа поведет себя, если у нее нет прав на запись файла. Если мы жестко «зашьем» путь к файлу в код, то проверить это будет сложно. Поэтому давайте добавим возможность передавать пути через аргументы командной строки. Напишем метод `ParseArgs`, который пробегается по аргументам и смотрит, не передали ли мы ему что-то полезное.

```csharp
private static void ParseArgs(string[] args)
{
    for (int i = 0; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--result-path" when i + 1 < args.Length:
                ResultPath = args[++i];
                break;
            case "--log-path" when i + 1 < args.Length:
                LogPath = args[++i];
                break;
            case "--simulate-unexpected":
                SimulateUnexpected = true;
                break;
        }
    }
}
```

Теперь пишем главный метод `Main`. Обратите внимание, что он возвращает `int` (0 — успех, 1 — ошибка). Это полезно, если программу запускают скрипты или тесты — они сразу поймут, сработала она правильно или нет. В начале метода парсим аргументы и, если стоит флажок, искусственно бросаем исключение, чтобы проверить общий блок `catch`.

```csharp
private static int Main(string[] args)
{
    try
    {
        ParseArgs(args);

        // Искусственная ошибка для проверки catch(Exception)
        if (SimulateUnexpected)
            throw new InvalidOperationException("Тестовое непредвиденное исключение (--simulate-unexpected).");

        Console.Write("Введите первое число: ");
        int num1 = ReadInt("первое число");

        Console.Write("Введите второе число: ");
        int num2 = ReadInt("второе число");

        double result = Divide(num1, num2);
        Console.WriteLine($"Результат деления: {result}");

        WriteResultToFile(ResultPath, result);
        Console.WriteLine($"Результат успешно записан в файл: {ResultPath}");

        return 0;
    }
    // Блоки catch мы добавим на следующем шаге...
```

## 3. Реализуем безопасный ввод и деление

Теперь давайте разберемся с вводом чисел. Пишем метод `ReadInt`. Он не пытается сам справиться с проблемой (например, вывести сообщение об ошибке), он просто «выбрасывает» (`throw`) исключение наверх, в метод `Main`. А `Main` уже решит, что с этим делать.

Мы отдельно проверяем на пустоту (`string.IsNullOrWhiteSpace`), так как если пользователь нажмет Enter, `int.Parse` выдаст ошибку, но лучше дать ему точное сообщение. Если же строку ввести не удалось, мы оборачиваем исключение в новое, добавляя контекст — в каком именно поле произошла ошибка.

```csharp
private static int ReadInt(string fieldName)
{
    string? input = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(input))
        throw new FormatException($"Поле \"{fieldName}\" не может быть пустым.");

    try
    {
        return int.Parse(input, NumberStyles.Integer, CultureInfo.InvariantCulture);
    }
    catch (FormatException ex)
    {
        // Оборачиваем в новое исключение, чтобы добавить контекст (какое поле)
        throw new FormatException($"Поле \"{fieldName}\" должно быть целым числом.", ex);
    }
    catch (OverflowException ex)
    {
        throw new OverflowException($"Поле \"{fieldName}\" выходит за диапазон типа int.", ex);
    }
}
```

Далее метод `Divide`. В C# при делении `double` на ноль программа не упадет, а вернет бесконечность `Infinity`. По условию задачи нам нужна обработка деления на ноль, поэтому мы делаем это вручную: проверяем знаменатель и, если он равен нулю, сами бросаем `DivideByZeroException`.

```csharp
private static double Divide(int numerator, int denominator)
{
    // Явная проверка нужна: (double)/0 дал бы Infinity, а не исключение.
    if (denominator == 0)
        throw new DivideByZeroException("Делитель не может быть равен нулю.");
    return (double)numerator / denominator;
}
```

И метод для записи в файл. Он максимально простой — просто пишет текст. Все ошибки, если они возникнут, полетят наверх в `Main`.

```csharp
private static void WriteResultToFile(string path, double result)
{
    File.WriteAllText(path, $"Результат деления: {result}");
}
```

## 4. Обработка исключений и логирование

Возвращаемся в наш метод `Main`. Теперь нам нужно «поймать» все то, что мы набросали. Главное правило: **блоки `catch` пишутся от самых специфичных к самым общим**. 

Обратите внимание на комментарий про `DirectoryNotFoundException`. Это исключение наследуется от `IOException`. Если поставить `IOException` первым, он перехватит ошибку каталога, и специфичный блок для каталога никогда не сработает.

```csharp
        return 0;
    }
    catch (FormatException ex)
    {
        HandleException(ex, "Введите корректное целое число");
        return 1;
    }
    catch (OverflowException ex)
    {
        HandleException(ex, "Число выходит за допустимый диапазон int");
        return 1;
    }
    catch (DivideByZeroException ex)
    {
        HandleException(ex, "Деление на ноль недопустимо");
        return 1;
    }
    // DirectoryNotFoundException наследует IOException — обязательно раньше!
    catch (DirectoryNotFoundException ex)
    {
        HandleException(ex, "Каталог для записи файла не найден");
        return 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        HandleException(ex, "Нет прав на запись файла");
        return 1;
    }
    catch (IOException ex)
    {
        HandleException(ex, "Ошибка ввода-вывода при работе с файлом");
        return 1;
    }
    catch (Exception ex)
    {
        HandleException(ex, "Непредвиденная ошибка");
        return 1;
    }
    finally
    {
        // Блок finally выполнится ВСЕГДА, даже если было исключение
        Console.WriteLine("Программа завершена.");
        if (!Console.IsInputRedirected)
            Console.ReadKey();
    }
}
```

Теперь напишем сам «обработчик» — метод `HandleException`, который выводит понятное сообщение пользователю, и метод `LogException`, который пишет в файл. 

Здесь есть важный нюанс: **а что, если само логирование упадет?** Например, нет прав на запись `app.log`. Если мы не обернем запись в `try/catch`, программа упадет с новой ошибкой, и мы потеряем информацию о той, которую пытались записать. Поэтому мы ловим эту ошибку внутри и выводим ее в консоль, не скрывая исходную.

```csharp
private static void HandleException(Exception ex, string userMessage)
{
    // Выводим сообщение пользователю
    Console.Error.WriteLine($"Ошибка: {userMessage}. Подробности: {ex.Message}");
    LogException(ex);
}

private static void LogException(Exception ex)
{
    string logRecord =
        $"{DateTime.Now:O} | {ex.GetType().FullName} | {ex.Message}" +
        Environment.NewLine +
        ex.StackTrace +
        Environment.NewLine +
        new string('-', 80) +
        Environment.NewLine;

    try
    {
        File.AppendAllText(LogPath, logRecord);
    }
    catch (Exception logEx)
    {
        // Если лог не записался — выводим ошибку в консоль, НО не скрываем исходную!
        Console.Error.WriteLine($"Не удалось записать лог в файл {LogPath}: {logEx.Message}");
        Console.Error.WriteLine($"Исходное исключение: {ex}");
    }
}
```

## 5. Тестирование программы

Теперь самое интересное — проверить, как наша программа «отбивает» атаки некорректных данных. Запускаем программу (лучше через командную строку, чтобы передавать аргументы) и проверяем каждый сценарий. В отчет по практике прикладываем скриншоты консоли и файла `app.log`.

| № | Ввод / Аргументы | Ожидаемое поведение | Где ловится | Запись в `app.log` | Сообщение в консоли |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 1 | `10`, `2` | Успех. Файл `result.txt` создан. | — | Нет | «Результат успешно записан» |
| 2 | `abc`, `2` | Ошибка формата | `FormatException` | Да | «Введите корректное целое число» |
| 3 | `[Enter]`, `2` | Пустой ввод | `FormatException` | Да | «Введите корректное целое число» |
| 4 | `99999999999999`, `2` | Переполнение | `OverflowException` | Да | «выходит за допустимый диапазон int» |
| 5 | `10`, `0` | Деление на ноль | `DivideByZeroException`| Да | «Деление на ноль недопустимо» |
| 6 | `10`, `2`, `--result-path C:\Windows\System32\test.txt` | Нет прав | `UnauthorizedAccessException` | Да | «Нет прав на запись файла» |
| 7 | `10`, `2`, `--result-path Z:\nope\test.txt` | Нет каталога | `DirectoryNotFoundException` | Да | «Каталог для записи файла не найден» |
| 8 | `--simulate-unexpected` | Непредвиденная ошибка | `Exception` (последний) | Да | «Непредвиденная ошибка» |

---

### Вопросы для защиты (на что обратить внимание студентов)
1. Почему `DirectoryNotFoundException` в коде написан раньше, чем `IOException`? *(Потому что он является его наследником. Если написать наоборот, то `IOException` перехватит ошибку каталога, и специфичный catch не сработает).*
2. Почему метод `Divide` выбрасывает исключение вручную, а не дает это сделать среде? *(Потому что мы приводим типы к `double`. Деление `double` на ноль не вызывает ошибку, а возвращает бесконечность `Infinity`).*
3. Зачем нужен `try/catch` внутри метода `LogException`? *(Если упадет запись лога, программа выбросит новое исключение, и мы потеряем информацию о той ошибке, которую изначально пытались залогировать. Мы не скрываем ошибку молча, а пишем её в консоль).*
