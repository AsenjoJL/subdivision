using System.ComponentModel.DataAnnotations;
using Google.Cloud.Firestore;

namespace HOMEOWNER.Models
{
    [FirestoreData]
    public class Reservation
    {
        private string _startTimeValue = "00:00:00";
        private string _endTimeValue = "00:00:00";

        [FirestoreProperty]
        [Key]
        public int ReservationID { get; set; }

        [FirestoreProperty]
        public int HomeownerID { get; set; }

        [FirestoreProperty]
        public int FacilityID { get; set; }

        [FirestoreProperty]
        public DateTime ReservationDate { get; set; }

        [FirestoreProperty("StartTime")]
        public string StartTimeValue
        {
            get => _startTimeValue;
            set => _startTimeValue = string.IsNullOrWhiteSpace(value) ? "00:00:00" : value;
        }

        public TimeSpan StartTime
        {
            get => TimeSpan.TryParse(StartTimeValue, out var parsed) ? parsed : TimeSpan.Zero;
            set => StartTimeValue = value.ToString(@"hh\:mm\:ss");
        }

        [FirestoreProperty("EndTime")]
        public string EndTimeValue
        {
            get => _endTimeValue;
            set => _endTimeValue = string.IsNullOrWhiteSpace(value) ? "00:00:00" : value;
        }

        public TimeSpan EndTime
        {
            get => TimeSpan.TryParse(EndTimeValue, out var parsed) ? parsed : TimeSpan.Zero;
            set => EndTimeValue = value.ToString(@"hh\:mm\:ss");
        }

        [FirestoreProperty]
        public string? Status { get; set; }

        [FirestoreProperty]
        public string? Purpose { get; set; }

        [FirestoreProperty]
        public DateTime CreatedAt { get; set; }

        [FirestoreProperty]
        public DateTime UpdatedAt { get; set; }

        [FirestoreProperty]
        public int? Rating { get; set; }

        // Navigation properties (not stored in Firestore)
        public Homeowner? Homeowner { get; set; }
        public Facility? Facility { get; set; }
    }
}
