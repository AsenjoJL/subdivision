using HOMEOWNER.Data;
using HOMEOWNER.Models;
using HOMEOWNER.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace HOMEOWNER.Controllers
{
    [Authorize]
    public class VehicleRegistrationController : BaseController
    {
        private static readonly List<VehicleOptionGroup> BrandGroups =
        [
            new VehicleOptionGroup
            {
                Label = "Car brands",
                Options =
                [
                    "Toyota",
                    "Honda",
                    "Mitsubishi",
                    "Nissan",
                    "Ford",
                    "Chevrolet",
                    "Hyundai",
                    "Kia",
                    "Mazda",
                    "Subaru",
                    "Suzuki",
                    "Isuzu",
                    "Volkswagen",
                    "BMW",
                    "Mercedes-Benz",
                    "Audi",
                    "Lexus",
                    "Volvo",
                    "Land Rover",
                    "Jeep",
                    "Tesla"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Motorcycle brands",
                Options =
                [
                    "Honda",
                    "Yamaha",
                    "Suzuki",
                    "Kawasaki",
                    "KTM",
                    "Ducati",
                    "Harley-Davidson",
                    "BMW Motorrad",
                    "Vespa",
                    "Royal Enfield"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Pickup / truck brands",
                Options =
                [
                    "Isuzu",
                    "Hino",
                    "Fuso",
                    "Mitsubishi",
                    "Ford",
                    "Toyota"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Common in the Philippines",
                Options =
                [
                    "Toyota",
                    "Mitsubishi",
                    "Honda",
                    "Nissan",
                    "Suzuki",
                    "Hyundai",
                    "Kia",
                    "Ford",
                    "Mazda",
                    "Isuzu"
                ]
            }
        ];

        private static readonly List<VehicleOptionGroup> ModelGroups =
        [
            new VehicleOptionGroup
            {
                Label = "Toyota",
                Options =
                [
                    "Vios",
                    "Corolla",
                    "Corolla Cross",
                    "Camry",
                    "Wigo",
                    "Raize",
                    "Avanza",
                    "Innova",
                    "Fortuner",
                    "Hilux",
                    "Land Cruiser"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Honda",
                Options =
                [
                    "Civic",
                    "City",
                    "Brio",
                    "Jazz",
                    "HR-V",
                    "CR-V",
                    "Accord",
                    "Mobilio"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Mitsubishi",
                Options =
                [
                    "Mirage",
                    "Mirage G4",
                    "Lancer",
                    "Xpander",
                    "Montero Sport",
                    "Strada"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Nissan",
                Options =
                [
                    "Almera",
                    "Sentra",
                    "Livina",
                    "Terra",
                    "Navara"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Ford",
                Options =
                [
                    "Ranger",
                    "Everest",
                    "Territory",
                    "Mustang"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Suzuki",
                Options =
                [
                    "Swift",
                    "Celerio",
                    "Ertiga",
                    "Jimny",
                    "XL7"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Hyundai",
                Options =
                [
                    "Accent",
                    "Elantra",
                    "Tucson",
                    "Santa Fe",
                    "Stargazer"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Kia",
                Options =
                [
                    "Soluto",
                    "Seltos",
                    "Sportage",
                    "Carnival"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Mazda",
                Options =
                [
                    "Mazda 2",
                    "Mazda 3",
                    "CX-3",
                    "CX-5",
                    "BT-50"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Isuzu",
                Options =
                [
                    "D-Max",
                    "MU-X"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Honda motorcycles",
                Options =
                [
                    "Click 125",
                    "Click 160",
                    "PCX",
                    "ADV160",
                    "Wave"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Yamaha motorcycles",
                Options =
                [
                    "NMAX",
                    "Aerox",
                    "Mio",
                    "Sniper"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Suzuki motorcycles",
                Options =
                [
                    "Raider",
                    "Burgman"
                ]
            },
            new VehicleOptionGroup
            {
                Label = "Kawasaki motorcycles",
                Options =
                [
                    "Ninja",
                    "Rouser"
                ]
            }
        ];

        private static readonly List<string> ColorOptions =
        [
            "White",
            "Black",
            "Gray",
            "Silver",
            "Red",
            "Blue",
            "Green",
            "Yellow",
            "Orange",
            "Brown",
            "Gold",
            "Beige",
            "Maroon",
            "Purple",
            "Pink"
        ];

        private static readonly HashSet<string> ManageableStatuses = new(StringComparer.OrdinalIgnoreCase)
        {
            "Approved",
            "Rejected"
        };

        public VehicleRegistrationController(IDataService data) : base(data)
        {
        }

        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> Register()
        {
            return PartialView("Register", await BuildHomeownerViewModelAsync());
        }

        [Authorize(Roles = "Homeowner")]
        [HttpPost]
        public async Task<IActionResult> Register(VehicleRegistrationViewModel registration)
        {
            if (!ModelState.IsValid)
            {
                return Json(new
                {
                    success = false,
                    message = "Invalid data provided.",
                    validationErrors = GetValidationErrors()
                });
            }

            var homeownerId = GetCurrentHomeownerId();
            if (homeownerId == 0)
            {
                return Json(new { success = false, message = "Homeowner not found." });
            }

            var plateNumber = registration.PlateNumber.Trim().ToUpperInvariant();
            var existingVehicle = await _data.GetVehicleByPlateNumberAsync(plateNumber);
            if (existingVehicle != null)
            {
                return Json(new
                {
                    success = false,
                    message = "This plate number is already registered.",
                    validationErrors = new Dictionary<string, string[]>
                    {
                        ["PlateNumber"] = new[] { "This plate number is already registered." }
                    }
                });
            }

            var vehicle = new VehicleRegistration
            {
                HomeownerID = homeownerId,
                PlateNumber = plateNumber,
                VehicleType = registration.VehicleType.Trim(),
                Make = string.IsNullOrWhiteSpace(registration.Make) ? null : registration.Make.Trim(),
                Model = string.IsNullOrWhiteSpace(registration.Model) ? null : registration.Model.Trim(),
                Color = string.IsNullOrWhiteSpace(registration.Color) ? null : registration.Color.Trim(),
                Status = "Pending",
                RegisteredAt = DateTime.UtcNow
            };

            await _data.AddVehicleAsync(vehicle);

            return Json(new
            {
                success = true,
                message = "Vehicle registration submitted successfully! Awaiting admin approval.",
                vehicleId = vehicle.VehicleID
            });
        }

        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> MyVehicles()
        {
            return PartialView("Register", await BuildHomeownerViewModelAsync());
        }

        [Authorize(Roles = "Homeowner")]
        [HttpGet]
        public async Task<IActionResult> LoadMyVehicleCards()
        {
            var homeownerId = GetCurrentHomeownerId();
            var vehicles = await GetOrderedHomeownerVehiclesAsync(homeownerId);
            return PartialView("_HomeownerVehicleCards", vehicles);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> Manage()
        {
            return PartialView("Manage", await BuildAdminViewModelAsync());
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> LoadAdminVehicleCards()
        {
            var vehicles = await GetOrderedAdminVehiclesAsync();
            return PartialView("_AdminVehicleCards", vehicles);
        }

        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<IActionResult> GetVehicleDetails(int id)
        {
            var vehicle = await _data.GetVehicleByIdAsync(id);
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Vehicle registration not found." });
            }

            var homeowner = vehicle.Homeowner ?? await _data.GetHomeownerByIdAsync(vehicle.HomeownerID);

            return Json(new
            {
                success = true,
                vehicle = new
                {
                    id = vehicle.VehicleID,
                    plateNumber = vehicle.PlateNumber,
                    vehicleType = vehicle.VehicleType,
                    make = string.IsNullOrWhiteSpace(vehicle.Make) ? "-" : vehicle.Make,
                    model = string.IsNullOrWhiteSpace(vehicle.Model) ? "-" : vehicle.Model,
                    color = string.IsNullOrWhiteSpace(vehicle.Color) ? "-" : vehicle.Color,
                    status = vehicle.Status,
                    registeredAt = vehicle.RegisteredAt.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt"),
                    approvedAt = vehicle.ApprovedAt?.ToLocalTime().ToString("MMM dd, yyyy hh:mm tt") ?? "-",
                    expiryDate = vehicle.ExpiryDate?.ToLocalTime().ToString("MMM dd, yyyy") ?? "-",
                    adminNotes = string.IsNullOrWhiteSpace(vehicle.AdminNotes) ? "-" : vehicle.AdminNotes,
                    homeownerName = homeowner?.FullName ?? "Unknown homeowner",
                    homeownerBlockLot = homeowner?.BlockLotNumber ?? "-"
                }
            });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, string status, DateTime? expiryDate = null, string? notes = null)
        {
            if (string.IsNullOrWhiteSpace(status) || !ManageableStatuses.Contains(status))
            {
                return Json(new { success = false, message = "Invalid vehicle registration status." });
            }

            var vehicle = await _data.GetVehicleByIdAsync(id);
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Vehicle registration not found." });
            }

            var email = User.FindFirstValue(ClaimTypes.Email);
            var admin = await _data.GetAdminByEmailAsync(email ?? string.Empty);

            vehicle.Status = status.Trim();
            vehicle.AdminNotes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

            if (vehicle.Status.Equals("Approved", StringComparison.OrdinalIgnoreCase))
            {
                vehicle.ApprovedAt = DateTime.UtcNow;
                vehicle.ApprovedByAdminID = admin?.AdminID ?? 1;
                vehicle.ExpiryDate = expiryDate?.Date ?? DateTime.UtcNow.Date.AddYears(1);
            }
            else
            {
                vehicle.ApprovedAt = null;
                vehicle.ApprovedByAdminID = null;
                vehicle.ExpiryDate = null;
            }

            await _data.UpdateVehicleAsync(vehicle);

            return Json(new { success = true, message = $"Vehicle registration {vehicle.Status.ToLowerInvariant()} successfully!" });
        }

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var vehicle = await _data.GetVehicleByIdAsync(id);
            if (vehicle == null)
            {
                return Json(new { success = false, message = "Vehicle registration not found." });
            }

            await _data.DeleteVehicleAsync(id);
            return Json(new { success = true, message = "Vehicle registration deleted successfully!" });
        }

        private async Task<HomeownerVehicleRegistrationsViewModel> BuildHomeownerViewModelAsync()
        {
            var homeownerId = GetCurrentHomeownerId();
            var vehicles = await GetOrderedHomeownerVehiclesAsync(homeownerId);
            var now = DateTime.Now;

            return new HomeownerVehicleRegistrationsViewModel
            {
                NewVehicle = new VehicleRegistrationViewModel(),
                Vehicles = vehicles,
                BrandGroups = BrandGroups,
                ModelGroups = ModelGroups,
                ColorOptions = ColorOptions,
                TotalVehicles = vehicles.Count,
                PendingVehicles = vehicles.Count(vehicle => vehicle.Status == "Pending"),
                ApprovedVehicles = vehicles.Count(vehicle => vehicle.Status == "Approved"),
                ExpiringSoonVehicles = vehicles.Count(vehicle =>
                    vehicle.Status == "Approved" &&
                    vehicle.ExpiryDate.HasValue &&
                    vehicle.ExpiryDate.Value.ToLocalTime().Date <= now.Date.AddDays(30))
            };
        }

        private async Task<AdminVehicleRegistrationManagementViewModel> BuildAdminViewModelAsync()
        {
            var vehicles = await GetOrderedAdminVehiclesAsync();
            var now = DateTime.Now.Date;

            return new AdminVehicleRegistrationManagementViewModel
            {
                Vehicles = vehicles,
                TotalVehicles = vehicles.Count,
                PendingVehicles = vehicles.Count(vehicle => vehicle.Status == "Pending"),
                ApprovedVehicles = vehicles.Count(vehicle => vehicle.Status == "Approved"),
                ExpiredVehicles = vehicles.Count(vehicle =>
                    vehicle.Status == "Approved" &&
                    vehicle.ExpiryDate.HasValue &&
                    vehicle.ExpiryDate.Value.ToLocalTime().Date < now),
                Statuses = new List<string> { "All", "Pending", "Approved", "Rejected", "Expired" }
            };
        }

        private async Task<List<VehicleRegistration>> GetOrderedHomeownerVehiclesAsync(int homeownerId)
        {
            return (await _data.GetVehiclesByHomeownerIdAsync(homeownerId))
                .OrderByDescending(vehicle => vehicle.RegisteredAt)
                .ToList();
        }

        private async Task<List<VehicleRegistration>> GetOrderedAdminVehiclesAsync()
        {
            return (await _data.GetVehicleRegistrationsAsync())
                .OrderByDescending(vehicle => vehicle.RegisteredAt)
                .ToList();
        }

        private Dictionary<string, string[]> GetValidationErrors()
        {
            return ModelState
                .Where(entry => entry.Value?.Errors.Count > 0)
                .ToDictionary(
                    entry => entry.Key,
                    entry => entry.Value!.Errors.Select(error => error.ErrorMessage).ToArray());
        }
    }
}
