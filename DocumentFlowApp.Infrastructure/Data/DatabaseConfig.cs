using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DocumentFlowApp.Infrastructure.Data
{
    // Конфигурация подключения к базе данных
    public static class DatabaseConfig
    {
        public static string GetConnectionString()
        {
            return "Host=localhost;Port=5432;Database=DocumentFlowApp;Username=postgres;Password=qwerty;";
        }
    }
}
