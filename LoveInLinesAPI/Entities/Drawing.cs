using Postgrest.Attributes;
using Postgrest.Models;

namespace LoveInLinesAPI.Entities
{
    [Table("drawings")]
    public class Drawing : BaseModel
    {
        [PrimaryKey("id", false)]
        public int Id { get; set; }

        [Column("image_url")]
        public string ImageURL { get; set; }

        [Column("total_likes")]
        public int TotalLikes { get; set; }

        [Column("user_id")]
        public string UserId { get; set; }
        
        [Column("user_profile_pic")]
        public string UserProfilePic { get; set; }

    }
}
