using HOMEOWNER.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

[Authorize(Roles = "Homeowner")]
public class ServiceController : Controller
{
    public ServiceController(IDataService data)
    {
    }

    public IActionResult SubmitRequest()
    {
        return RedirectToAction("SubmitRequest", "Homeowner");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SubmitRequest(object request)
    {
        return RedirectToAction("SubmitRequest", "Homeowner");
    }

    public IActionResult ViewRequests()
    {
        return RedirectToAction("SubmitRequest", "Homeowner");
    }
}
