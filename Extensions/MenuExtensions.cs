using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace AdminPannel.Extensions
{
    public static class MenuExtensions
    {
        public static List<MenuItem> GetAllMenuItems(this Menu menu)
        {
            List<MenuItem> menuItems = new List<MenuItem>();
            GetMenuItemsRecursive(menu.Items, menuItems);
            return menuItems;
        }

        private static void GetMenuItemsRecursive(ItemCollection items, List<MenuItem> menuItems)
        {
            foreach (object item in items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItems.Add(menuItem);
                    GetMenuItemsRecursive(menuItem.Items, menuItems);
                }
            }
        }


        public static void CheckMenuItemAndParents(this MenuItem menuItem)
        {
            menuItem.IsChecked = true;

            CheckParentMenuItems(menuItem.Parent as MenuItem);
        }

        private static void CheckParentMenuItems(MenuItem? parent)
        {
            if (parent != null)
            {
                parent.IsChecked = true;
                CheckParentMenuItems(parent.Parent as MenuItem);
            }
        }


        public static void UncheckAllMenuItems(this Menu menu)
        {
            UncheckMenuItemsRecursive(menu.Items);
        }

        private static void UncheckMenuItemsRecursive(ItemCollection items)
        {
            foreach (object item in items)
            {
                if (item is MenuItem menuItem)
                {
                    menuItem.IsChecked = false;
                    UncheckMenuItemsRecursive(menuItem.Items);
                }
            }
        }

    }

}
