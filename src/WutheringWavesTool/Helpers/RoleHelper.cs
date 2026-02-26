using System.Security.Cryptography.X509Certificates;

namespace Haiyu.Helpers;

public static class RoleHelper
{
    extension(int RoleData)
    {
        public string SwitchType()
        {
            var TypeImage = "";
            switch (RoleData)
            {
                case 1:
                    TypeImage = GameIcon.Icon1;
                    break;
                case 2:
                    TypeImage = GameIcon.Icon2;
                    break;
                case 3:
                    TypeImage = GameIcon.Icon3;
                    break;
                case 4:
                    TypeImage = GameIcon.Icon4;
                    break;
                case 5:
                    TypeImage = GameIcon.Icon5;
                    break;
                case 6:
                    TypeImage = GameIcon.Icon6;
                    break;
            }
            return TypeImage;
        }
    }

}