using DocumentFlowApp.Core.Interfaces;
using DocumentFlowApp.Infrastructure.Data;
using DocumentFlowApp.Infrastructure.Repositories;
using DocumentFlowApp.Services;
using DocumentFlowApp.WPF.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace DocumentFlowApp.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider _serviceProvider;

    public App()
    {
        // 🎯 Глобальный обработчик исключений
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        try
        {
            // Настройка Dependency Injection
            var services = new ServiceCollection();
            ConfigureServices(services);
            _serviceProvider = services.BuildServiceProvider();
        }
        catch (Exception ex)
        {
            ShowError("Ошибка при настройке DI контейнера", ex);
            Shutdown();
        }
    }

    private void ConfigureServices(ServiceCollection services)
    {
        // 📊 Регистрируем DbContext
        services.AddDbContext<ApplicationDbContext>(options =>
        {
            options.UseNpgsql(DatabaseConfig.GetConnectionString());
            options.EnableSensitiveDataLogging();
            options.EnableDetailedErrors();
        });

        // 📁 Регистрируем репозитории
        services.AddScoped<IDocumentRepository, DocumentRepository>();

        // 🧠 Регистрируем сервисы
        services.AddScoped<IDocumentService, DocumentService>();

        // 🎯 Регистрируем ViewModel
        services.AddTransient<MainViewModel>();

        // 🪟 Регистрируем главное окно
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 🟡 ВРЕМЕННОЕ РЕШЕНИЕ: создаем все вручную (надежнее)
            var connectionString = DatabaseConfig.GetConnectionString();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;

            // Создаем цепочку зависимостей вручную
            var context = new ApplicationDbContext(options);
            var repository = new DocumentRepository(context);
            var service = new DocumentService(repository);
            var viewModel = new MainViewModel(service);

            // Создаем и показываем главное окно
            var mainWindow = new MainWindow(viewModel);
            mainWindow.Show();

            MessageBox.Show("✅ Приложение запущено успешно!", "Успех",
                          MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowError("Ошибка при запуске приложения", ex);
            Shutdown();
        }
    }

    // 🎯 Глобальные обработчики исключений
    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        ShowError("Необработанное исключение в UI", e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        ShowError("Необработанное исключение в приложении", e.ExceptionObject as Exception);
    }

    private void ShowError(string title, Exception ex)
    {
        string message = $"{title}:\n\n" +
                       $"Сообщение: {ex?.Message}\n\n" +
                       $"Тип: {ex?.GetType().Name}\n\n" +
                       $"StackTrace: {ex?.StackTrace}";

        if (ex?.InnerException != null)
        {
            message += $"\n\nInnerException: {ex.InnerException.Message}";
        }

        MessageBox.Show(message, "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}


