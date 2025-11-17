using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace DocumentFlowApp.Infrastructure.Data
{
    public static class DatabaseTester
    {
        public static async Task <bool> TestConnectionAsync()
        {
            try
            {
                Console.WriteLine("Проверка подключения к PostgreSQL...");

                using var context = new ApplicationDbContext();
                var canConnect = await context.Database.CanConnectAsync();

                if (canConnect)
                {
                    Console.WriteLine("Подключение к PostgreSQL успешно!");
                    Console.WriteLine($"База данных: {context.Database.GetDbConnection().Database}");
                    return true;
                }
                else
                {
                    Console.WriteLine("Не удалось подключиться к базе данных");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка подключения: {ex.Message}");
                Console.WriteLine($"Детали: {ex.InnerException?.Message}");
                return false;
            }
        }
    }
}
