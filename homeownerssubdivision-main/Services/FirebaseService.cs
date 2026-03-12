using Google.Cloud.Firestore;
using HOMEOWNER.Data;
using HOMEOWNER.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace HOMEOWNER.Services
{
    public class FirebaseService : IDataService
    {
        private readonly FirestoreDb _db;

        public FirebaseService(IConfiguration configuration)
        {
            try
            {
                var projectId = configuration["Firebase:ProjectId"] ?? "homeowner-c355d";
                _db = FirestoreDb.Create(projectId);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Firebase credentials not found. Please set GOOGLE_APPLICATION_CREDENTIALS environment variable " +
                    "to point to your Firebase service account JSON file. " +
                    "See HOW_TO_RUN.md for instructions. Error: " + ex.Message, ex);
            }
        }

        // Collections
        private CollectionReference HomeownersCollection => _db.Collection("homeowners");
        private CollectionReference AdminsCollection => _db.Collection("admins");
        private CollectionReference StaffCollection => _db.Collection("staff");
        private CollectionReference FacilitiesCollection => _db.Collection("facilities");
        private CollectionReference ReservationsCollection => _db.Collection("reservations");
        private CollectionReference ServiceRequestsCollection => _db.Collection("serviceRequests");
        private CollectionReference AnnouncementsCollection => _db.Collection("announcements");
        private CollectionReference ForumPostsCollection => _db.Collection("forumPosts");
        private CollectionReference ForumCommentsCollection => _db.Collection("forumComments");
        private CollectionReference ReactionsCollection => _db.Collection("reactions");
        private CollectionReference EventsCollection => _db.Collection("events");
        private CollectionReference NotificationsCollection => _db.Collection("notifications");
        private CollectionReference CommunitySettingsCollection => _db.Collection("communitySettings");
        private CollectionReference HomeownerProfileImagesCollection => _db.Collection("homeownerProfileImages");
        private CollectionReference BillingsCollection => _db.Collection("billings");
        private CollectionReference DocumentsCollection => _db.Collection("documents");
        private CollectionReference ContactsCollection => _db.Collection("contacts");
        private CollectionReference VisitorPassesCollection => _db.Collection("visitorPasses");
        private CollectionReference VehicleRegistrationsCollection => _db.Collection("vehicleRegistrations");
        private CollectionReference GateAccessLogsCollection => _db.Collection("gateAccessLogs");
        private CollectionReference ComplaintsCollection => _db.Collection("complaints");
        private CollectionReference PollsCollection => _db.Collection("polls");
        private CollectionReference PollOptionsCollection => _db.Collection("pollOptions");
        private CollectionReference PollVotesCollection => _db.Collection("pollVotes");
        private CollectionReference AdminWorkspaceSettingsCollection => _db.Collection("adminWorkspaceSettings");
        private CollectionReference CountersCollection => _db.Collection("systemCounters");

        private static DateTime EnsureUtc(DateTime value)
        {
            return value.Kind == DateTimeKind.Utc ? value : value.ToUniversalTime();
        }

        private static DateTime NormalizeEventDateForStorage(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
            {
                return value;
            }

            if (value.Kind == DateTimeKind.Unspecified)
            {
                value = DateTime.SpecifyKind(value, DateTimeKind.Local);
            }

            return value.ToUniversalTime();
        }

        private static EventModel NormalizeEventForDisplay(EventModel eventModel)
        {
            if (eventModel.EventDate.Kind == DateTimeKind.Utc)
            {
                eventModel.EventDate = eventModel.EventDate.ToLocalTime();
            }

            return eventModel;
        }

        private static VisitorPass NormalizeVisitorPassForStorage(VisitorPass visitorPass)
        {
            var visitDate = visitorPass.VisitDate;
            if (visitDate.Kind == DateTimeKind.Unspecified)
            {
                visitDate = DateTime.SpecifyKind(visitDate.Date, DateTimeKind.Local);
            }
            else
            {
                visitDate = visitDate.Date;
            }

            visitorPass.VisitDate = visitDate.ToUniversalTime();
            visitorPass.RequestedAt = EnsureUtc(visitorPass.RequestedAt);
            visitorPass.ApprovedAt = visitorPass.ApprovedAt.HasValue ? EnsureUtc(visitorPass.ApprovedAt.Value) : null;
            visitorPass.CheckedInAt = visitorPass.CheckedInAt.HasValue ? EnsureUtc(visitorPass.CheckedInAt.Value) : null;
            visitorPass.CheckedOutAt = visitorPass.CheckedOutAt.HasValue ? EnsureUtc(visitorPass.CheckedOutAt.Value) : null;
            return visitorPass;
        }

        private static VisitorPass NormalizeVisitorPassForDisplay(VisitorPass visitorPass)
        {
            if (visitorPass.VisitDate.Kind == DateTimeKind.Utc)
            {
                visitorPass.VisitDate = visitorPass.VisitDate.ToLocalTime();
            }

            if (visitorPass.RequestedAt.Kind == DateTimeKind.Utc)
            {
                visitorPass.RequestedAt = visitorPass.RequestedAt.ToLocalTime();
            }

            if (visitorPass.ApprovedAt.HasValue && visitorPass.ApprovedAt.Value.Kind == DateTimeKind.Utc)
            {
                visitorPass.ApprovedAt = visitorPass.ApprovedAt.Value.ToLocalTime();
            }

            if (visitorPass.CheckedInAt.HasValue && visitorPass.CheckedInAt.Value.Kind == DateTimeKind.Utc)
            {
                visitorPass.CheckedInAt = visitorPass.CheckedInAt.Value.ToLocalTime();
            }

            if (visitorPass.CheckedOutAt.HasValue && visitorPass.CheckedOutAt.Value.Kind == DateTimeKind.Utc)
            {
                visitorPass.CheckedOutAt = visitorPass.CheckedOutAt.Value.ToLocalTime();
            }

            return visitorPass;
        }

        private static VehicleRegistration NormalizeVehicleForStorage(VehicleRegistration vehicle)
        {
            vehicle.RegisteredAt = EnsureUtc(vehicle.RegisteredAt);
            vehicle.ApprovedAt = vehicle.ApprovedAt.HasValue ? EnsureUtc(vehicle.ApprovedAt.Value) : null;
            vehicle.ExpiryDate = vehicle.ExpiryDate.HasValue
                ? NormalizeEventDateForStorage(vehicle.ExpiryDate.Value)
                : null;
            return vehicle;
        }

        private static VehicleRegistration NormalizeVehicleForDisplay(VehicleRegistration vehicle)
        {
            if (vehicle.RegisteredAt.Kind == DateTimeKind.Utc)
            {
                vehicle.RegisteredAt = vehicle.RegisteredAt.ToLocalTime();
            }

            if (vehicle.ApprovedAt.HasValue && vehicle.ApprovedAt.Value.Kind == DateTimeKind.Utc)
            {
                vehicle.ApprovedAt = vehicle.ApprovedAt.Value.ToLocalTime();
            }

            if (vehicle.ExpiryDate.HasValue && vehicle.ExpiryDate.Value.Kind == DateTimeKind.Utc)
            {
                vehicle.ExpiryDate = vehicle.ExpiryDate.Value.ToLocalTime();
            }

            return vehicle;
        }

        private async Task<int> GetNextSequenceValueAsync(string counterName)
        {
            var counterDocument = CountersCollection.Document(counterName);

            return await _db.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(counterDocument);
                var currentValue = 0L;

                if (snapshot.Exists && snapshot.TryGetValue<long>("Value", out var storedValue))
                {
                    currentValue = storedValue;
                }

                var nextValue = currentValue + 1;
                transaction.Set(counterDocument, new Dictionary<string, object>
                {
                    ["Value"] = nextValue,
                    ["UpdatedAt"] = Timestamp.GetCurrentTimestamp()
                });

                return checked((int)nextValue);
            });
        }

        private async Task<IReadOnlyList<int>> GetNextSequenceValuesAsync(string counterName, int count)
        {
            if (count <= 0)
            {
                return Array.Empty<int>();
            }

            var counterDocument = CountersCollection.Document(counterName);

            return await _db.RunTransactionAsync(async transaction =>
            {
                var snapshot = await transaction.GetSnapshotAsync(counterDocument);
                var currentValue = 0L;

                if (snapshot.Exists && snapshot.TryGetValue<long>("Value", out var storedValue))
                {
                    currentValue = storedValue;
                }

                var startValue = currentValue + 1;
                var endValue = currentValue + count;

                transaction.Set(counterDocument, new Dictionary<string, object>
                {
                    ["Value"] = endValue,
                    ["UpdatedAt"] = Timestamp.GetCurrentTimestamp()
                });

                return Enumerable.Range(checked((int)startValue), count).ToArray();
            });
        }

        private async Task DeleteDocumentsAsync(IEnumerable<DocumentReference> documents)
        {
            const int batchSize = 400;
            var batch = _db.StartBatch();
            var operations = 0;

            foreach (var document in documents)
            {
                batch.Delete(document);
                operations++;

                if (operations >= batchSize)
                {
                    await batch.CommitAsync();
                    batch = _db.StartBatch();
                    operations = 0;
                }
            }

            if (operations > 0)
            {
                await batch.CommitAsync();
            }
        }

        private async Task<ServiceRequest> PopulateServiceRequestReferencesAsync(ServiceRequest request)
        {
            request.Homeowner = await GetHomeownerByIdAsync(request.HomeownerID);

            if (request.AssignedStaffID.HasValue)
            {
                request.AssignedStaff = await GetStaffByIdAsync(request.AssignedStaffID.Value);
            }

            return request;
        }

        private async Task<List<ServiceRequest>> PopulateServiceRequestReferencesAsync(List<ServiceRequest> requests)
        {
            var homeowners = (await GetHomeownersAsync()).ToDictionary(h => h.HomeownerID);
            var staff = (await GetStaffAsync()).ToDictionary(s => s.StaffID);

            foreach (var request in requests)
            {
                homeowners.TryGetValue(request.HomeownerID, out var homeowner);
                request.Homeowner = homeowner;

                if (request.AssignedStaffID.HasValue &&
                    staff.TryGetValue(request.AssignedStaffID.Value, out var assignedStaff))
                {
                    request.AssignedStaff = assignedStaff;
                }
            }

            return requests;
        }

        private async Task<List<VehicleRegistration>> PopulateVehicleReferencesAsync(List<VehicleRegistration> vehicles)
        {
            if (vehicles.Count == 0)
            {
                return vehicles;
            }

            var homeownerIds = vehicles.Select(vehicle => vehicle.HomeownerID).Distinct().ToHashSet();
            var homeowners = (await GetHomeownersAsync())
                .Where(homeowner => homeownerIds.Contains(homeowner.HomeownerID))
                .ToDictionary(homeowner => homeowner.HomeownerID);

            foreach (var vehicle in vehicles)
            {
                homeowners.TryGetValue(vehicle.HomeownerID, out var homeowner);
                vehicle.Homeowner = homeowner;
            }

            return vehicles;
        }

        private async Task<Reservation> PopulateReservationReferencesAsync(Reservation reservation)
        {
            reservation.Homeowner = await GetHomeownerByIdAsync(reservation.HomeownerID);
            reservation.Facility = await GetFacilityByIdAsync(reservation.FacilityID);
            return reservation;
        }

        private async Task<List<Reservation>> PopulateReservationReferencesAsync(List<Reservation> reservations)
        {
            var homeowners = (await GetHomeownersAsync()).ToDictionary(h => h.HomeownerID);
            var facilities = (await GetFacilitiesAsync()).ToDictionary(f => f.FacilityID);

            foreach (var reservation in reservations)
            {
                homeowners.TryGetValue(reservation.HomeownerID, out var homeowner);
                facilities.TryGetValue(reservation.FacilityID, out var facility);
                reservation.Homeowner = homeowner;
                reservation.Facility = facility;
            }

            return reservations;
        }

        private async Task<ForumPost> PopulateForumPostReferencesAsync(ForumPost post)
        {
            var homeownerTask = GetHomeownerByIdAsync(post.HomeownerID);
            var commentsTask = ForumCommentsCollection
                .WhereEqualTo("ForumPostID", post.ForumPostID)
                .GetSnapshotAsync();
            var reactionsTask = ReactionsCollection
                .WhereEqualTo("ForumPostID", post.ForumPostID)
                .GetSnapshotAsync();

            await Task.WhenAll(homeownerTask, commentsTask, reactionsTask);

            post.Homeowner = homeownerTask.Result;
            post.Comments = commentsTask.Result.Documents
                .Select(doc => doc.ConvertTo<ForumComment>())
                .OrderBy(comment => comment.CreatedAt)
                .ToList();
            post.Reactions = reactionsTask.Result.Documents
                .Select(doc => doc.ConvertTo<Reaction>())
                .ToList();

            var homeownerIds = post.Comments
                .Select(comment => comment.HomeownerID)
                .Append(post.HomeownerID)
                .Distinct()
                .ToList();

            var homeowners = (await GetHomeownersAsync())
                .Where(homeowner => homeownerIds.Contains(homeowner.HomeownerID))
                .ToDictionary(homeowner => homeowner.HomeownerID);

            if (homeowners.TryGetValue(post.HomeownerID, out var postHomeowner))
            {
                post.Homeowner = postHomeowner;
            }

            foreach (var comment in post.Comments)
            {
                homeowners.TryGetValue(comment.HomeownerID, out var commentHomeowner);
                comment.Homeowner = commentHomeowner;
            }

            return post;
        }

        private async Task<List<ForumPost>> PopulateForumPostReferencesAsync(List<ForumPost> posts)
        {
            var homeowners = (await GetHomeownersAsync()).ToDictionary(h => h.HomeownerID);
            var comments = await GetForumCommentsAsync();
            var reactions = await GetReactionsAsync();

            var commentsByPost = comments
                .GroupBy(c => c.ForumPostID)
                .ToDictionary(g => g.Key, g => g.ToList());

            var reactionsByPost = reactions
                .GroupBy(r => r.ForumPostID)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var post in posts)
            {
                homeowners.TryGetValue(post.HomeownerID, out var homeowner);
                post.Homeowner = homeowner;

                if (commentsByPost.TryGetValue(post.ForumPostID, out var postComments))
                {
                    foreach (var comment in postComments)
                    {
                        homeowners.TryGetValue(comment.HomeownerID, out var commentHomeowner);
                        comment.Homeowner = commentHomeowner;
                    }

                    post.Comments = postComments;
                }
                else
                {
                    post.Comments = new List<ForumComment>();
                }

                post.Reactions = reactionsByPost.TryGetValue(post.ForumPostID, out var postReactions)
                    ? postReactions
                    : new List<Reaction>();
            }

            return posts;
        }

        // Homeowners
        public async Task<List<Homeowner>> GetHomeownersAsync()
        {
            var snapshot = await HomeownersCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Homeowner>()).ToList();
        }

        public async Task<Homeowner?> GetHomeownerByIdAsync(int id)
        {
            var doc = await HomeownersCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Homeowner>() : null;
        }

        public async Task<Homeowner?> GetHomeownerByEmailAsync(string email)
        {
            var query = HomeownersCollection.WhereEqualTo("Email", email);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<Homeowner>();
        }

        public async Task AddHomeownerAsync(Homeowner homeowner)
        {
            if (homeowner.HomeownerID == 0)
            {
                homeowner.HomeownerID = await GetNextSequenceValueAsync("homeowners");
            }

            homeowner.CreatedAt = EnsureUtc(homeowner.CreatedAt);
            await HomeownersCollection.Document(homeowner.HomeownerID.ToString()).SetAsync(homeowner);
        }

        public async Task UpdateHomeownerAsync(Homeowner homeowner)
        {
            await HomeownersCollection.Document(homeowner.HomeownerID.ToString()).SetAsync(homeowner);
        }

        public async Task DeleteHomeownerAsync(int id)
        {
            await HomeownersCollection.Document(id.ToString()).DeleteAsync();
        }

        public async Task<int> GetHomeownerCountAsync(string role = "Homeowner")
        {
            var query = HomeownersCollection.WhereEqualTo("Role", role);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Count;
        }

        // Admins
        public async Task<List<Admin>> GetAdminsAsync()
        {
            var snapshot = await AdminsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Admin>()).ToList();
        }

        public async Task<Admin?> GetAdminByIdAsync(int id)
        {
            var doc = await AdminsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Admin>() : null;
        }

        public async Task<Admin?> GetAdminByEmailAsync(string email)
        {
            var query = AdminsCollection.WhereEqualTo("Email", email);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<Admin>();
        }

        public async Task AddAdminAsync(Admin admin)
        {
            if (admin.AdminID == 0)
            {
                admin.AdminID = await GetNextSequenceValueAsync("admins");
            }

            await AdminsCollection.Document(admin.AdminID.ToString()).SetAsync(admin);
        }

        public async Task UpdateAdminAsync(Admin admin)
        {
            await AdminsCollection.Document(admin.AdminID.ToString()).SetAsync(admin);
        }

        public async Task<AdminWorkspaceSettings?> GetAdminWorkspaceSettingsAsync()
        {
            var doc = await AdminWorkspaceSettingsCollection.Document("1").GetSnapshotAsync();
            if (!doc.Exists)
            {
                return null;
            }

            var settings = doc.ConvertTo<AdminWorkspaceSettings>();
            return settings;
        }

        public async Task AddOrUpdateAdminWorkspaceSettingsAsync(AdminWorkspaceSettings settings)
        {
            settings.AdminWorkspaceSettingsID = 1;
            settings.LastUpdated = EnsureUtc(settings.LastUpdated == default ? DateTime.UtcNow : settings.LastUpdated);
            await AdminWorkspaceSettingsCollection.Document("1").SetAsync(settings);
        }

        // Staff
        public async Task<List<Staff>> GetStaffAsync()
        {
            var snapshot = await StaffCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Staff>()).ToList();
        }

        public async Task<Staff?> GetStaffByIdAsync(int id)
        {
            var doc = await StaffCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Staff>() : null;
        }

        public async Task<Staff?> GetStaffByEmailAsync(string email)
        {
            var query = StaffCollection.WhereEqualTo("Email", email);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<Staff>();
        }

        public async Task<List<Staff>> GetStaffByPositionAsync(string position)
        {
            var query = StaffCollection.WhereEqualTo("Position", position);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Staff>()).ToList();
        }

        public async Task AddStaffAsync(Staff staff)
        {
            // Generate StaffID if not set
            if (staff.StaffID == 0)
            {
                staff.StaffID = await GetNextSequenceValueAsync("staff");
            }

            // Ensure CreatedAt is UTC for Firestore
            if (staff.CreatedAt == default(DateTime) || staff.CreatedAt.Kind != DateTimeKind.Utc)
            {
                staff.CreatedAt = DateTime.UtcNow;
            }

            await StaffCollection.Document(staff.StaffID.ToString()).SetAsync(staff);
        }

        public async Task UpdateStaffAsync(Staff staff)
        {
            await StaffCollection.Document(staff.StaffID.ToString()).SetAsync(staff);
        }

        public async Task DeleteStaffAsync(int id)
        {
            await StaffCollection.Document(id.ToString()).DeleteAsync();
        }

        public async Task<int> GetStaffCountAsync()
        {
            var snapshot = await StaffCollection.GetSnapshotAsync();
            return snapshot.Count;
        }

        // Facilities
        public async Task<List<Facility>> GetFacilitiesAsync()
        {
            var snapshot = await FacilitiesCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Facility>()).ToList();
        }

        public async Task<List<Facility>> GetAvailableFacilitiesAsync()
        {
            var query = FacilitiesCollection.WhereEqualTo("AvailabilityStatus", "Available");
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Facility>()).ToList();
        }

        public async Task<Facility?> GetFacilityByIdAsync(int id)
        {
            var doc = await FacilitiesCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Facility>() : null;
        }

        public async Task AddFacilityAsync(Facility facility)
        {
            // Auto-generate FacilityID if not set
            if (facility.FacilityID == 0)
            {
                facility.FacilityID = await GetNextSequenceValueAsync("facilities");
            }
            await FacilitiesCollection.Document(facility.FacilityID.ToString()).SetAsync(facility);
        }

        public async Task UpdateFacilityAsync(Facility facility)
        {
            await FacilitiesCollection.Document(facility.FacilityID.ToString()).SetAsync(facility);
        }

        public async Task DeleteFacilityAsync(int id)
        {
            await FacilitiesCollection.Document(id.ToString()).DeleteAsync();
        }

        // Reservations
        public async Task<List<Reservation>> GetReservationsAsync()
        {
            var snapshot = await ReservationsCollection.GetSnapshotAsync();
            var reservations = snapshot.Documents.Select(doc => doc.ConvertTo<Reservation>()).ToList();
            return await PopulateReservationReferencesAsync(reservations);
        }

        public async Task<List<Reservation>> GetReservationsByHomeownerIdAsync(int homeownerId)
        {
            var query = ReservationsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            var reservations = snapshot.Documents.Select(doc => doc.ConvertTo<Reservation>()).ToList();
            return await PopulateReservationReferencesAsync(reservations);
        }

        public async Task<List<Reservation>> GetReservationsByStatusAsync(string status)
        {
            var query = ReservationsCollection.WhereEqualTo("Status", status);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Reservation>()).ToList();
        }

        public async Task<int> GetReservationCountByHomeownerIdAndStatusAsync(int homeownerId, string status)
        {
            var query = ReservationsCollection
                .WhereEqualTo("HomeownerID", homeownerId)
                .WhereEqualTo("Status", status);

            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Count;
        }

        public async Task<Reservation?> GetReservationByIdAsync(int id)
        {
            var doc = await ReservationsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists)
            {
                return null;
            }

            return await PopulateReservationReferencesAsync(doc.ConvertTo<Reservation>());
        }

        public async Task AddReservationAsync(Reservation reservation)
        {
            if (reservation.ReservationID == 0)
            {
                reservation.ReservationID = await GetNextSequenceValueAsync("reservations");
            }

            reservation.ReservationDate = EnsureUtc(reservation.ReservationDate);
            reservation.CreatedAt = EnsureUtc(reservation.CreatedAt);
            reservation.UpdatedAt = EnsureUtc(reservation.UpdatedAt);
            await ReservationsCollection.Document(reservation.ReservationID.ToString()).SetAsync(reservation);
        }

        public async Task UpdateReservationAsync(Reservation reservation)
        {
            await ReservationsCollection.Document(reservation.ReservationID.ToString()).SetAsync(reservation);
        }

        public async Task DeleteReservationAsync(int id)
        {
            await ReservationsCollection.Document(id.ToString()).DeleteAsync();
        }

        public async Task<int> GetReservationCountByStatusAsync(string status)
        {
            var query = ReservationsCollection.WhereEqualTo("Status", status);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Count;
        }

        // Service Requests
        public async Task<List<ServiceRequest>> GetServiceRequestsAsync()
        {
            var snapshot = await ServiceRequestsCollection.GetSnapshotAsync();
            var requests = snapshot.Documents.Select(doc => doc.ConvertTo<ServiceRequest>()).ToList();
            return await PopulateServiceRequestReferencesAsync(requests);
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsByHomeownerIdAsync(int homeownerId)
        {
            var query = ServiceRequestsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            var requests = snapshot.Documents.Select(doc => doc.ConvertTo<ServiceRequest>()).ToList();
            return await PopulateServiceRequestReferencesAsync(requests);
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsForHomeownerDashboardAsync(int homeownerId)
        {
            var query = ServiceRequestsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();

            return snapshot.Documents
                .Select(doc => doc.ConvertTo<ServiceRequest>())
                .OrderByDescending(request => request.CreatedAt)
                .ToList();
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsByStatusAsync(string status)
        {
            var query = ServiceRequestsCollection.WhereEqualTo("Status", status);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<ServiceRequest>()).ToList();
        }

        public async Task<int> GetOpenServiceRequestCountByHomeownerIdAsync(int homeownerId)
        {
            var requests = await GetServiceRequestsForHomeownerDashboardAsync(homeownerId);

            return requests.Count(r =>
                !string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(r.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(r.Status, "Canceled", StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<ServiceRequest>> GetServiceRequestsByCategoryAsync(string category)
        {
            var query = ServiceRequestsCollection.WhereEqualTo("Category", category);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<ServiceRequest>()).ToList();
        }

        public async Task<ServiceRequest?> GetServiceRequestByIdAsync(int id)
        {
            var doc = await ServiceRequestsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists)
            {
                return null;
            }

            return await PopulateServiceRequestReferencesAsync(doc.ConvertTo<ServiceRequest>());
        }

        public async Task AddServiceRequestAsync(ServiceRequest request)
        {
            if (request.RequestID == 0)
            {
                request.RequestID = await GetNextSequenceValueAsync("serviceRequests");
            }

            request.CreatedAt = EnsureUtc(request.CreatedAt);
            await ServiceRequestsCollection.Document(request.RequestID.ToString()).SetAsync(request);
        }

        public async Task UpdateServiceRequestAsync(ServiceRequest request)
        {
            await ServiceRequestsCollection.Document(request.RequestID.ToString()).SetAsync(request);
        }

        public async Task DeleteServiceRequestAsync(int id)
        {
            await ServiceRequestsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Announcements
        public async Task<List<Announcement>> GetAnnouncementsAsync()
        {
            var snapshot = await AnnouncementsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Announcement>()).OrderByDescending(a => a.PostedAt).ToList();
        }

        public async Task<Announcement?> GetAnnouncementByIdAsync(int id)
        {
            var doc = await AnnouncementsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Announcement>() : null;
        }

        public async Task AddAnnouncementAsync(Announcement announcement)
        {
            if (announcement.AnnouncementID == 0)
            {
                announcement.AnnouncementID = await GetNextSequenceValueAsync("announcements");
            }

            announcement.PostedAt = EnsureUtc(announcement.PostedAt);
            await AnnouncementsCollection.Document(announcement.AnnouncementID.ToString()).SetAsync(announcement);
        }

        public async Task UpdateAnnouncementAsync(Announcement announcement)
        {
            await AnnouncementsCollection.Document(announcement.AnnouncementID.ToString()).SetAsync(announcement);
        }

        public async Task DeleteAnnouncementAsync(int id)
        {
            await AnnouncementsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Forum Posts
        public async Task<List<ForumPost>> GetForumPostsAsync()
        {
            var snapshot = await ForumPostsCollection.GetSnapshotAsync();
            var posts = snapshot.Documents.Select(doc => doc.ConvertTo<ForumPost>()).ToList();
            await PopulateForumPostReferencesAsync(posts);
            return posts.OrderByDescending(p => p.CreatedAt).ToList();
        }

        public async Task<ForumPost?> GetForumPostByIdAsync(int id)
        {
            var doc = await ForumPostsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists) return null;
            
            var post = doc.ConvertTo<ForumPost>();
            return await PopulateForumPostReferencesAsync(post);
        }

        public async Task<DateTime?> GetLatestForumActivityAsync()
        {
            var latestPostTask = GetLatestCreatedAtAsync(ForumPostsCollection);
            var latestCommentTask = GetLatestCreatedAtAsync(ForumCommentsCollection);
            var latestReactionTask = GetLatestCreatedAtAsync(ReactionsCollection);

            await Task.WhenAll(latestPostTask, latestCommentTask, latestReactionTask);

            return new[]
            {
                latestPostTask.Result,
                latestCommentTask.Result,
                latestReactionTask.Result
            }
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .DefaultIfEmpty()
            .Max();
        }

        public async Task AddForumPostAsync(ForumPost post)
        {
            if (post.ForumPostID == 0)
            {
                post.ForumPostID = await GetNextSequenceValueAsync("forumPosts");
            }

            post.CreatedAt = EnsureUtc(post.CreatedAt);
            await ForumPostsCollection.Document(post.ForumPostID.ToString()).SetAsync(post);
        }

        public async Task UpdateForumPostAsync(ForumPost post)
        {
            await ForumPostsCollection.Document(post.ForumPostID.ToString()).SetAsync(post);
        }

        public async Task DeleteForumPostAsync(int id)
        {
            await ForumPostsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Forum Comments
        public async Task AddForumCommentAsync(ForumComment comment)
        {
            if (comment.ForumCommentID == 0)
            {
                comment.ForumCommentID = await GetNextSequenceValueAsync("forumComments");
            }

            comment.CreatedAt = EnsureUtc(comment.CreatedAt);
            await ForumCommentsCollection.Document(comment.ForumCommentID.ToString()).SetAsync(comment);
        }

        public async Task DeleteForumCommentAsync(int id)
        {
            await ForumCommentsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Reactions
        public async Task AddReactionAsync(Reaction reaction)
        {
            if (reaction.ReactionID == 0)
            {
                reaction.ReactionID = await GetNextSequenceValueAsync("reactions");
            }

            reaction.CreatedAt = EnsureUtc(reaction.CreatedAt);
            await ReactionsCollection.Document(reaction.ReactionID.ToString()).SetAsync(reaction);
        }

        public async Task DeleteReactionAsync(int id)
        {
            await ReactionsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Events
        public async Task<List<EventModel>> GetEventsAsync()
        {
            var snapshot = await EventsCollection.GetSnapshotAsync();
            return snapshot.Documents
                .Select(doc => NormalizeEventForDisplay(doc.ConvertTo<EventModel>()))
                .OrderBy(e => e.EventDate)
                .ToList();
        }

        public async Task<EventModel?> GetEventByIdAsync(int id)
        {
            var doc = await EventsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? NormalizeEventForDisplay(doc.ConvertTo<EventModel>()) : null;
        }

        public async Task AddEventAsync(EventModel eventModel)
        {
            // Auto-generate EventID if not set
            if (eventModel.EventID == 0)
            {
                eventModel.EventID = await GetNextSequenceValueAsync("events");
            }
            eventModel.EventDate = NormalizeEventDateForStorage(eventModel.EventDate);
            await EventsCollection.Document(eventModel.EventID.ToString()).SetAsync(eventModel);
        }

        public async Task UpdateEventAsync(EventModel eventModel)
        {
            eventModel.EventDate = NormalizeEventDateForStorage(eventModel.EventDate);
            await EventsCollection.Document(eventModel.EventID.ToString()).SetAsync(eventModel);
        }

        public async Task DeleteEventAsync(int id)
        {
            await EventsCollection.Document(id.ToString()).DeleteAsync();
        }

        // Community Settings
        public async Task<CommunitySettings?> GetCommunitySettingsAsync()
        {
            var snapshot = await CommunitySettingsCollection.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<CommunitySettings>();
        }

        public async Task AddOrUpdateCommunitySettingsAsync(CommunitySettings settings)
        {
            await CommunitySettingsCollection.Document("settings").SetAsync(settings);
        }

        // Homeowner Profile Images
        public async Task<HomeownerProfileImage?> GetHomeownerProfileImageAsync(int homeownerId)
        {
            var query = HomeownerProfileImagesCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<HomeownerProfileImage>();
        }

        public async Task<List<HomeownerProfileImage>> GetHomeownerProfileImagesAsync()
        {
            var snapshot = await HomeownerProfileImagesCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<HomeownerProfileImage>()).ToList();
        }

        public async Task AddOrUpdateHomeownerProfileImageAsync(HomeownerProfileImage image)
        {
            image.UploadedAt = EnsureUtc(image.UploadedAt);
            image.LastUpdatedDate = EnsureUtc(image.LastUpdatedDate);
            var query = HomeownerProfileImagesCollection.WhereEqualTo("HomeownerID", image.HomeownerID);
            var snapshot = await query.GetSnapshotAsync();
            var existingDoc = snapshot.Documents.FirstOrDefault();

            if (existingDoc != null)
            {
                await existingDoc.Reference.SetAsync(image);
            }
            else
            {
                await HomeownerProfileImagesCollection.Document().SetAsync(image);
            }
        }

        // Notifications
        public async Task<List<Notification>> GetNotificationsAsync()
        {
            var snapshot = await NotificationsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Notification>()).ToList();
        }

        public async Task AddNotificationAsync(Notification notification)
        {
            if (notification.NotificationID == 0)
            {
                notification.NotificationID = await GetNextSequenceValueAsync("notifications");
            }

            notification.CreatedAt = EnsureUtc(notification.CreatedAt);
            await NotificationsCollection.Document(notification.NotificationID.ToString()).SetAsync(notification);
        }

        public async Task<int> GetNotificationCountAsync()
        {
            var snapshot = await NotificationsCollection.GetSnapshotAsync();
            return snapshot.Count;
        }

        // IDataService Implementation - IQueryable properties
        public IQueryable<Homeowner> Homeowners => GetHomeownersAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Admin> Admins => GetAdminsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Staff> Staff => GetStaffAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Facility> Facilities => GetFacilitiesAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Reservation> Reservations => GetReservationsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<ServiceRequest> ServiceRequests => GetServiceRequestsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Announcement> Announcements => GetAnnouncementsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<ForumPost> ForumPosts => GetForumPostsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<ForumComment> ForumComments => GetForumCommentsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<Reaction> Reactions => GetReactionsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<EventModel> Events => GetEventsAsync().GetAwaiter().GetResult().AsQueryable();
        public IQueryable<HomeownerProfileImage> HomeownerProfileImages => GetHomeownerProfileImagesAsync().GetAwaiter().GetResult().AsQueryable();

        // Billing
        public async Task<List<Billing>> GetBillingsAsync()
        {
            var snapshot = await BillingsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Billing>()).ToList();
        }

        public async Task<Billing?> GetBillingByIdAsync(int id)
        {
            var doc = await BillingsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Billing>() : null;
        }

        public async Task<List<Billing>> GetBillingsByHomeownerIdAsync(int homeownerId)
        {
            var query = BillingsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Billing>()).ToList();
        }

        public async Task AddBillingAsync(Billing billing)
        {
            // Auto-generate BillingID if not set
            if (billing.BillingID == 0)
            {
                billing.BillingID = await GetNextSequenceValueAsync("billings");
            }
            await BillingsCollection.Document(billing.BillingID.ToString()).SetAsync(billing);
        }

        public async Task UpdateBillingAsync(Billing billing)
        {
            await BillingsCollection.Document(billing.BillingID.ToString()).SetAsync(billing);
        }

        public async Task DeleteBillingAsync(int id)
        {
            await BillingsCollection.Document(id.ToString()).DeleteAsync();
        }

        public IQueryable<Billing> Billings => GetBillingsAsync().GetAwaiter().GetResult().AsQueryable();

        // Documents
        public async Task<List<Document>> GetDocumentsAsync()
        {
            var snapshot = await DocumentsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Document>()).ToList();
        }

        public async Task<Document?> GetDocumentByIdAsync(int id)
        {
            var doc = await DocumentsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Document>() : null;
        }

        public async Task<List<Document>> GetDocumentsByCategoryAsync(string category)
        {
            var query = DocumentsCollection.WhereEqualTo("Category", category);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Document>()).ToList();
        }

        public async Task AddDocumentAsync(Document document)
        {
            if (document.DocumentID == 0)
            {
                var allDocs = await GetDocumentsAsync();
                document.DocumentID = allDocs.Any() ? allDocs.Max(d => d.DocumentID) + 1 : 1;
            }
            await DocumentsCollection.Document(document.DocumentID.ToString()).SetAsync(document);
        }

        public async Task UpdateDocumentAsync(Document document)
        {
            await DocumentsCollection.Document(document.DocumentID.ToString()).SetAsync(document);
        }

        public async Task DeleteDocumentAsync(int id)
        {
            await DocumentsCollection.Document(id.ToString()).DeleteAsync();
        }

        public async Task IncrementDownloadCountAsync(int documentId)
        {
            var doc = await GetDocumentByIdAsync(documentId);
            if (doc != null)
            {
                doc.DownloadCount++;
                await UpdateDocumentAsync(doc);
            }
        }

        public IQueryable<Document> Documents => GetDocumentsAsync().GetAwaiter().GetResult().AsQueryable();

        // Contacts
        public async Task<List<Contact>> GetContactsAsync()
        {
            var snapshot = await ContactsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Contact>()).ToList();
        }

        public async Task<Contact?> GetContactByIdAsync(int id)
        {
            var doc = await ContactsCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<Contact>() : null;
        }

        public async Task<List<Contact>> GetContactsByCategoryAsync(string category)
        {
            var query = ContactsCollection.WhereEqualTo("Category", category);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Contact>()).ToList();
        }

        public async Task AddContactAsync(Contact contact)
        {
            if (contact.ContactID == 0)
            {
                contact.ContactID = await GetNextSequenceValueAsync("contacts");
            }
            await ContactsCollection.Document(contact.ContactID.ToString()).SetAsync(contact);
        }

        public async Task UpdateContactAsync(Contact contact)
        {
            await ContactsCollection.Document(contact.ContactID.ToString()).SetAsync(contact);
        }

        public async Task DeleteContactAsync(int id)
        {
            await ContactsCollection.Document(id.ToString()).DeleteAsync();
        }

        public IQueryable<Contact> Contacts => GetContactsAsync().GetAwaiter().GetResult().AsQueryable();

        // Visitor Passes
        public async Task<List<VisitorPass>> GetVisitorPassesAsync()
        {
            var snapshot = await VisitorPassesCollection.GetSnapshotAsync();
            return snapshot.Documents
                .Select(doc => NormalizeVisitorPassForDisplay(doc.ConvertTo<VisitorPass>()))
                .ToList();
        }

        public async Task<VisitorPass?> GetVisitorPassByIdAsync(int id)
        {
            var doc = await VisitorPassesCollection.Document(id.ToString()).GetSnapshotAsync();
            return doc.Exists ? NormalizeVisitorPassForDisplay(doc.ConvertTo<VisitorPass>()) : null;
        }

        public async Task<List<VisitorPass>> GetVisitorPassesByHomeownerIdAsync(int homeownerId)
        {
            var query = VisitorPassesCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents
                .Select(doc => NormalizeVisitorPassForDisplay(doc.ConvertTo<VisitorPass>()))
                .ToList();
        }

        public async Task AddVisitorPassAsync(VisitorPass visitorPass)
        {
            if (visitorPass.VisitorPassID == 0)
            {
                visitorPass.VisitorPassID = await GetNextSequenceValueAsync("visitorPasses");
            }
            await VisitorPassesCollection.Document(visitorPass.VisitorPassID.ToString()).SetAsync(
                NormalizeVisitorPassForStorage(visitorPass));
        }

        public async Task UpdateVisitorPassAsync(VisitorPass visitorPass)
        {
            await VisitorPassesCollection.Document(visitorPass.VisitorPassID.ToString()).SetAsync(
                NormalizeVisitorPassForStorage(visitorPass));
        }

        public async Task DeleteVisitorPassAsync(int id)
        {
            await VisitorPassesCollection.Document(id.ToString()).DeleteAsync();
        }

        public IQueryable<VisitorPass> VisitorPasses => GetVisitorPassesAsync().GetAwaiter().GetResult().AsQueryable();

        // Vehicle Registration
        public async Task<List<VehicleRegistration>> GetVehicleRegistrationsAsync()
        {
            var snapshot = await VehicleRegistrationsCollection.GetSnapshotAsync();
            var vehicles = snapshot.Documents
                .Select(doc => NormalizeVehicleForDisplay(doc.ConvertTo<VehicleRegistration>()))
                .ToList();
            return await PopulateVehicleReferencesAsync(vehicles);
        }

        public async Task<VehicleRegistration?> GetVehicleByIdAsync(int id)
        {
            var doc = await VehicleRegistrationsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists)
            {
                return null;
            }

            var vehicle = NormalizeVehicleForDisplay(doc.ConvertTo<VehicleRegistration>());
            vehicle.Homeowner = await GetHomeownerByIdAsync(vehicle.HomeownerID);
            return vehicle;
        }

        public async Task<List<VehicleRegistration>> GetVehiclesByHomeownerIdAsync(int homeownerId)
        {
            var query = VehicleRegistrationsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents
                .Select(doc => NormalizeVehicleForDisplay(doc.ConvertTo<VehicleRegistration>()))
                .OrderByDescending(vehicle => vehicle.RegisteredAt)
                .ToList();
        }

        public async Task<VehicleRegistration?> GetVehicleByPlateNumberAsync(string plateNumber)
        {
            var query = VehicleRegistrationsCollection.WhereEqualTo("PlateNumber", plateNumber);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.FirstOrDefault()?.ConvertTo<VehicleRegistration>();
        }

        public async Task AddVehicleAsync(VehicleRegistration vehicle)
        {
            if (vehicle.VehicleID == 0)
            {
                vehicle.VehicleID = await GetNextSequenceValueAsync("vehicleRegistrations");
            }
            await VehicleRegistrationsCollection.Document(vehicle.VehicleID.ToString()).SetAsync(
                NormalizeVehicleForStorage(vehicle));
        }

        public async Task UpdateVehicleAsync(VehicleRegistration vehicle)
        {
            await VehicleRegistrationsCollection.Document(vehicle.VehicleID.ToString()).SetAsync(
                NormalizeVehicleForStorage(vehicle));
        }

        public async Task DeleteVehicleAsync(int id)
        {
            await VehicleRegistrationsCollection.Document(id.ToString()).DeleteAsync();
        }

        public IQueryable<VehicleRegistration> VehicleRegistrations => GetVehicleRegistrationsAsync().GetAwaiter().GetResult().AsQueryable();

        // Gate Access Logs
        public async Task<List<GateAccessLog>> GetGateAccessLogsAsync()
        {
            var snapshot = await GateAccessLogsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<GateAccessLog>()).ToList();
        }

        public async Task AddGateAccessLogAsync(GateAccessLog log)
        {
            if (log.LogID == 0)
            {
                log.LogID = await GetNextSequenceValueAsync("gateAccessLogs");
            }
            await GateAccessLogsCollection.Document(log.LogID.ToString()).SetAsync(log);
        }

        public async Task<List<GateAccessLog>> GetGateAccessLogsByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            var allLogs = await GetGateAccessLogsAsync();
            return allLogs.Where(l => l.AccessTime >= startDate && l.AccessTime <= endDate).ToList();
        }

        public IQueryable<GateAccessLog> GateAccessLogs => GetGateAccessLogsAsync().GetAwaiter().GetResult().AsQueryable();

        // Complaints
        public async Task<List<Complaint>> GetComplaintsAsync()
        {
            var snapshot = await ComplaintsCollection.GetSnapshotAsync();
            var complaints = snapshot.Documents.Select(doc => doc.ConvertTo<Complaint>()).ToList();
            return await PopulateComplaintReferencesAsync(complaints);
        }

        public async Task<Complaint?> GetComplaintByIdAsync(int id)
        {
            var doc = await ComplaintsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists)
            {
                return null;
            }

            var complaint = doc.ConvertTo<Complaint>();
            complaint.Homeowner = await GetHomeownerByIdAsync(complaint.HomeownerID);
            return complaint;
        }

        public async Task<List<Complaint>> GetComplaintsByHomeownerIdAsync(int homeownerId)
        {
            var query = ComplaintsCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            var complaints = snapshot.Documents.Select(doc => doc.ConvertTo<Complaint>()).ToList();
            return await PopulateComplaintReferencesAsync(complaints);
        }

        public async Task AddComplaintAsync(Complaint complaint)
        {
            if (complaint.ComplaintID == 0)
            {
                complaint.ComplaintID = await GetNextSequenceValueAsync("complaints");
            }
            await ComplaintsCollection.Document(complaint.ComplaintID.ToString()).SetAsync(complaint);
        }

        public async Task UpdateComplaintAsync(Complaint complaint)
        {
            await ComplaintsCollection.Document(complaint.ComplaintID.ToString()).SetAsync(complaint);
        }

        public async Task DeleteComplaintAsync(int id)
        {
            await ComplaintsCollection.Document(id.ToString()).DeleteAsync();
        }

        public IQueryable<Complaint> Complaints => GetComplaintsAsync().GetAwaiter().GetResult().AsQueryable();

        private async Task<List<Complaint>> PopulateComplaintReferencesAsync(List<Complaint> complaints)
        {
            if (complaints.Count == 0)
            {
                return complaints;
            }

            var homeownerIds = complaints
                .Select(complaint => complaint.HomeownerID)
                .Distinct()
                .ToHashSet();

            var homeowners = (await GetHomeownersAsync())
                .Where(homeowner => homeownerIds.Contains(homeowner.HomeownerID))
                .ToDictionary(homeowner => homeowner.HomeownerID);

            foreach (var complaint in complaints)
            {
                homeowners.TryGetValue(complaint.HomeownerID, out var homeowner);
                complaint.Homeowner = homeowner;
            }

            return complaints;
        }

        // Polls
        public async Task<List<Poll>> GetPollsAsync()
        {
            var snapshot = await PollsCollection.GetSnapshotAsync();
            var polls = snapshot.Documents.Select(doc => doc.ConvertTo<Poll>()).ToList();

            var hydrateTasks = polls.Select(async poll =>
            {
                poll.Options = await GetPollOptionsAsync(poll.PollID);
                poll.Votes = new List<PollVote>();
            });

            await Task.WhenAll(hydrateTasks);
            return polls;
        }

        public async Task<Poll?> GetPollByIdAsync(int id)
        {
            var doc = await PollsCollection.Document(id.ToString()).GetSnapshotAsync();
            if (!doc.Exists) return null;

            var poll = doc.ConvertTo<Poll>();
            poll.Options = await GetPollOptionsAsync(id);
            poll.Votes = new List<PollVote>();
            return poll;
        }

        public async Task<List<Poll>> GetActivePollsAsync()
        {
            var now = DateTime.UtcNow;
            var allPolls = await GetPollsAsync();
            return allPolls.Where(p => p.Status == "Active" && 
                (p.StartDate == null || p.StartDate <= now) &&
                (p.EndDate == null || p.EndDate >= now)).ToList();
        }

        public async Task AddPollAsync(Poll poll)
        {
            if (poll.PollID == 0)
            {
                poll.PollID = await GetNextSequenceValueAsync("polls");
            }

            var options = poll.Options ?? new List<PollOption>();
            var optionIds = await GetNextSequenceValuesAsync("pollOptions", options.Count);
            var batch = _db.StartBatch();

            batch.Set(PollsCollection.Document(poll.PollID.ToString()), poll);

            for (var index = 0; index < options.Count; index++)
            {
                var option = options[index];
                option.PollID = poll.PollID;
                option.OptionID = optionIds[index];
                batch.Set(PollOptionsCollection.Document(option.OptionID.ToString()), option);
            }

            await batch.CommitAsync();
        }

        public async Task UpdatePollAsync(Poll poll)
        {
            await PollsCollection.Document(poll.PollID.ToString()).SetAsync(poll);
        }

        public async Task DeletePollAsync(int id)
        {
            var voteSnapshotTask = PollVotesCollection.WhereEqualTo("PollID", id).GetSnapshotAsync();
            var optionSnapshotTask = PollOptionsCollection.WhereEqualTo("PollID", id).GetSnapshotAsync();

            await Task.WhenAll(voteSnapshotTask, optionSnapshotTask);

            var documentsToDelete = voteSnapshotTask.Result.Documents
                .Select(doc => doc.Reference)
                .Concat(optionSnapshotTask.Result.Documents.Select(doc => doc.Reference))
                .Append(PollsCollection.Document(id.ToString()))
                .ToList();

            await DeleteDocumentsAsync(documentsToDelete);
        }

        public async Task AddPollVoteAsync(PollVote vote)
        {
            if (vote.VoteID == 0)
            {
                vote.VoteID = await GetNextSequenceValueAsync("pollVotes");
            }
            await PollVotesCollection.Document(vote.VoteID.ToString()).SetAsync(vote);
            
            // Update option vote count
            var option = await GetPollOptionByIdAsync(vote.OptionID);
            if (option != null)
            {
                option.VoteCount++;
                await UpdatePollOptionAsync(option);
            }
            
            // Update poll total votes
            var pollDoc = await PollsCollection.Document(vote.PollID.ToString()).GetSnapshotAsync();
            if (pollDoc.Exists)
            {
                var poll = pollDoc.ConvertTo<Poll>();
                poll.TotalVotes++;
                await UpdatePollAsync(poll);
            }
        }

        public async Task<bool> HasHomeownerVotedAsync(int pollId, int homeownerId)
        {
            var query = PollVotesCollection
                .WhereEqualTo("PollID", pollId)
                .WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Count > 0;
        }

        public async Task<HashSet<int>> GetVotedPollIdsByHomeownerAsync(int homeownerId)
        {
            var query = PollVotesCollection.WhereEqualTo("HomeownerID", homeownerId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents
                .Select(doc => doc.ConvertTo<PollVote>().PollID)
                .ToHashSet();
        }

        public IQueryable<Poll> Polls => GetPollsAsync().GetAwaiter().GetResult().AsQueryable();

        // Helper methods for Polls
        private async Task<List<PollOption>> GetPollOptionsAsync(int pollId)
        {
            var query = PollOptionsCollection.WhereEqualTo("PollID", pollId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<PollOption>()).OrderBy(o => o.DisplayOrder).ToList();
        }

        private async Task<List<PollVote>> GetPollVotesAsync(int pollId)
        {
            var query = PollVotesCollection.WhereEqualTo("PollID", pollId);
            var snapshot = await query.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<PollVote>()).ToList();
        }

        private async Task<PollOption?> GetPollOptionByIdAsync(int optionId)
        {
            var doc = await PollOptionsCollection.Document(optionId.ToString()).GetSnapshotAsync();
            return doc.Exists ? doc.ConvertTo<PollOption>() : null;
        }

        private async Task AddPollOptionAsync(PollOption option)
        {
            if (option.OptionID == 0)
            {
                option.OptionID = await GetNextSequenceValueAsync("pollOptions");
            }
            await PollOptionsCollection.Document(option.OptionID.ToString()).SetAsync(option);
        }

        private async Task UpdatePollOptionAsync(PollOption option)
        {
            await PollOptionsCollection.Document(option.OptionID.ToString()).SetAsync(option);
        }

        // Helper methods for IQueryable properties
        private async Task<List<ForumComment>> GetForumCommentsAsync()
        {
            var snapshot = await ForumCommentsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<ForumComment>()).ToList();
        }

        private async Task<DateTime?> GetLatestCreatedAtAsync(CollectionReference collection)
        {
            var snapshot = await collection
                .OrderByDescending("CreatedAt")
                .Limit(1)
                .GetSnapshotAsync();

            var document = snapshot.Documents.FirstOrDefault();
            if (document == null || !document.Exists)
            {
                return null;
            }

            if (document.TryGetValue<Timestamp>("CreatedAt", out var createdAtTimestamp))
            {
                return createdAtTimestamp.ToDateTime();
            }

            if (document.TryGetValue<DateTime>("CreatedAt", out var createdAtDateTime))
            {
                return EnsureUtc(createdAtDateTime);
            }

            return null;
        }

        private async Task<List<Reaction>> GetReactionsAsync()
        {
            var snapshot = await ReactionsCollection.GetSnapshotAsync();
            return snapshot.Documents.Select(doc => doc.ConvertTo<Reaction>()).ToList();
        }

        // SaveChangesAsync - No-op for Firebase (operations are immediate)
        public Task<int> SaveChangesAsync()
        {
            return Task.FromResult(1);
        }
    }
}
