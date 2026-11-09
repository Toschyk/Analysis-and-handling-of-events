using System.Globalization;

internal static class Program
{
    private static string ResultPath =
        Path.Combine(AppContext.BaseDirectory, "result.txt");

    private static string LogPath =
        Path.Combine(AppContext.BaseDirectory, "app.log");

    private static bool SimulateUnexpected;

    private static int Main(string[] args)
    {
        try
        {
            ParseArgs(args);

            if (SimulateUnexpected)
                throw new InvalidOperationException(
                    "Тестовое непредвиденное исключение (--simulate-unexpected).");

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
            Console.WriteLine("Программа завершена.");
            if (!Console.IsInputRedirected)
                Console.ReadKey();
        }
    }

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

    private static int ReadInt(string fieldName)
    {
        string? input = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException(
                $"Поле \"{fieldName}\" не может быть пустым.");

        try
        {
            return int.Parse(
                input, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }
        catch (FormatException ex)
        {
            throw new FormatException(
                $"Поле \"{fieldName}\" должно быть целым числом.", ex);
        }
        catch (OverflowException ex)
        {
            throw new OverflowException(
                $"Поле \"{fieldName}\" выходит за диапазон типа int.", ex);
        }
    }

    private static double Divide(int numerator, int denominator)
    {
        // Явная проверка нужна: (double)/0 дал бы Infinity, а не исключение.
        if (denominator == 0)
            throw new DivideByZeroException("Делитель не может быть равен нулю.");
        return (double)numerator / denominator;
    }

    private static void WriteResultToFile(string path, double result)
    {
        File.WriteAllText(path, $"Результат деления: {result}");
    }

    private static void HandleException(Exception ex, string userMessage)
    {
        Console.Error.WriteLine(
            $"Ошибка: {userMessage}. Подробности: {ex.Message}");
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
            // Ошибка логирования НЕ должна скрывать основное исключение —
            // выводим обе в stderr, а не молча глотаем.
            Console.Error.WriteLine(
                $"Не удалось записать лог в файл {LogPath}: {logEx.Message}");
            Console.Error.WriteLine($"Исходное исключение: {ex}");
        }
    }
}