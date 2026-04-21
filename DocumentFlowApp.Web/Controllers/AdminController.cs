using DocumentFlowApp.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DocumentFlowApp.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public class AdminController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Routes()
    {
        ViewData["SectionTitle"] = "Шаблоны маршрутов";
        ViewData["SectionDescription"] = "Здесь администратор управляет шаблонами согласования и шагами маршрута.";
        return View("Section");
    }

    public IActionResult Nomenclature()
    {
        ViewData["SectionTitle"] = "Номенклатура дел";
        ViewData["SectionDescription"] = "Здесь администратор ведет дела номенклатуры и правила автопривязки.";
        return View("Section");
    }

    public IActionResult Audit()
    {
        ViewData["SectionTitle"] = "Журнал аудита";
        ViewData["SectionDescription"] = "Здесь администратор просматривает критичные действия пользователей и документов.";
        return View("Section");
    }
}
