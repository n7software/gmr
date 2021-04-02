namespace GmrLib.Models
{
    public partial class Civilization
    {
        public static Civilization Unknown
        {
            get
            {
                return
                    new Civilization
                    {
                        CivID = -1,
                        Name = "Other",
                        Leader = "DLC, Mod, or Unspecified",
                        ImageUrl = "~/Content/images/civ/unknown.png?2",
                        SmallImageUrl = "~/Content/images/civ/unknownsm.png?2",
                        BackgroundColor = "505050"
                    };
            }
        }
    }
}