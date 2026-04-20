using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CconcessionTrackerAPI.Models
{
    public class CTEFUser
    {
        [Key]
        [Column("usr_int_id")]
        public int usr_int_id { get; set; }

        [Column("usr_vch_name")]
        [MaxLength(200)]
        public string? usr_vch_name { get; set; }

        [Column("usr_vch_emailid")]
        [MaxLength(200)]
        public string? usr_vch_emailid { get; set; }

        [Column("usr_vch_pswd")]
        [MaxLength(500)]
        public string? usr_vch_pswd { get; set; }
    }
}
