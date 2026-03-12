using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class ContactController : BaseController
    {
        private static readonly string[] DefaultCategories =
        {
            "HOA Officer",
            "Security",
            "Maintenance",
            "Emergency",
            "Administration",
            "Utility",
            "Other"
        };

        public ContactController(IDataService data) : base(data)
        {
        }

        // View Contact Directory (All users)
        public async Task<IActionResult> Index()
        {
            var contacts = await GetOrderedContactsAsync(activeOnly: true);

            var viewModel = new HomeownerContactDirectoryViewModel
            {
                Contacts = contacts,
                Categories = BuildCategories(contacts, includeAll: true),
                TotalContacts = contacts.Count,
                EmergencyContacts = contacts.Count(c => c.IsEmergency)
            };

            return PartialView("Index", viewModel);
        }

        // Admin: Manage Contacts
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Manage()
        {
            var contacts = await GetOrderedContactsAsync();

            return PartialView("Manage", new AdminContactManagementViewModel
            {
                Contacts = contacts,
                Categories = BuildCategories(contacts),
                TotalContacts = contacts.Count,
                ActiveContacts = contacts.Count(c => c.IsActive),
                EmergencyContacts = contacts.Count(c => c.IsEmergency)
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> LoadContactCards()
        {
            var contacts = await GetOrderedContactsAsync();

            return PartialView("_AdminContactCards", contacts);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetContact(int id)
        {
            var contact = await _data.GetContactByIdAsync(id);
            if (contact == null)
            {
                return NotFound(new { success = false, message = "Contact not found." });
            }

            return Json(new
            {
                success = true,
                contact = new
                {
                    contact.ContactID,
                    contact.Name,
                    contact.Category,
                    contact.Position,
                    contact.PhoneNumber,
                    contact.MobileNumber,
                    contact.Email,
                    contact.OfficeLocation,
                    contact.Department,
                    contact.IsEmergency,
                    contact.IsActive,
                    contact.DisplayOrder
                }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Add([FromForm] ContactFormViewModel model)
        {
            if (!ModelState.IsValid || string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.Category))
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid data provided.",
                    errors = ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
                });
            }

            var contact = MapToContact(model);
            await _data.AddContactAsync(contact);
            return Json(new { success = true, message = "Contact added successfully!", contactId = contact.ContactID });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Edit([FromForm] ContactFormViewModel model)
        {
            if (!ModelState.IsValid || model.ContactID <= 0)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid data provided.",
                    errors = ModelState
                        .Where(entry => entry.Value?.Errors.Count > 0)
                        .ToDictionary(
                            entry => entry.Key,
                            entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray())
                });
            }

            var existing = await _data.GetContactByIdAsync(model.ContactID);
            if (existing == null)
            {
                return Json(new { success = false, message = "Contact not found." });
            }

            var contact = MapToContact(model);
            await _data.UpdateContactAsync(contact);
            return Json(new { success = true, message = "Contact updated successfully!" });
        }

        // Admin: Delete Contact
        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var contact = await _data.GetContactByIdAsync(id);
            if (contact == null)
            {
                return Json(new { success = false, message = "Contact not found." });
            }

            await _data.DeleteContactAsync(id);
            return Json(new { success = true, message = "Contact deleted successfully!" });
        }

        private static Contact MapToContact(ContactFormViewModel model)
        {
            return new Contact
            {
                ContactID = model.ContactID,
                Name = model.Name.Trim(),
                Category = model.Category.Trim(),
                Position = string.IsNullOrWhiteSpace(model.Position) ? null : model.Position.Trim(),
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                MobileNumber = string.IsNullOrWhiteSpace(model.MobileNumber) ? null : model.MobileNumber.Trim(),
                Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim(),
                OfficeLocation = string.IsNullOrWhiteSpace(model.OfficeLocation) ? null : model.OfficeLocation.Trim(),
                Department = string.IsNullOrWhiteSpace(model.Department) ? null : model.Department.Trim(),
                IsEmergency = model.IsEmergency,
                IsActive = model.IsActive,
                DisplayOrder = model.DisplayOrder
            };
        }

        private async Task<List<Contact>> GetOrderedContactsAsync(bool activeOnly = false)
        {
            var contacts = await _data.GetContactsAsync();
            var query = contacts.AsEnumerable();

            if (activeOnly)
            {
                query = query.Where(c => c.IsActive);
            }

            return query
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Category)
                .ThenBy(c => c.Name)
                .ToList();
        }

        private static List<string> BuildCategories(IEnumerable<Contact> contacts, bool includeAll = false)
        {
            var categories = DefaultCategories
                .Concat(contacts
                    .Select(c => c.Category)
                    .Where(c => !string.IsNullOrWhiteSpace(c)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => Array.IndexOf(DefaultCategories, c) >= 0 ? Array.IndexOf(DefaultCategories, c) : int.MaxValue)
                .ThenBy(c => c)
                .ToList();

            if (!includeAll)
            {
                return categories;
            }

            return new List<string> { "All" }
                .Concat(categories)
                .ToList();
        }
    }
}
