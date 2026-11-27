using DarkSunRE.Models;
using DSunTools.Common.Gff;
using DSunTools.Common.UI;
using System;
using System.IO;

namespace DarkSunRE.Services
{
    /// <summary>
    /// When an ICON used by a BUTN changes size, keep the button hitbox and
    /// its WIND placement in sync. The menu buttons are individually centred
    /// by their WIND x/y, so moving x/y by half the size delta keeps the
    /// visible caption centred after image import.
    /// </summary>
    public static class ButtonSizeSyncService
    {
        public static int ApplyIconSizeChange(
            Project project,
            ResourceFile container,
            ResourceInfo icon,
            int oldWidth,
            int oldHeight,
            int newWidth,
            int newHeight)
        {
            if (project == null || container?.Resources == null || icon == null)
                return 0;
            if (oldWidth == newWidth && oldHeight == newHeight)
                return 0;

            int deltaWidth = newWidth - oldWidth;
            int deltaHeight = newHeight - oldHeight;
            GffFile gff = GffFile.Create(container.Data);
            int changed = 0;

            foreach (ResourceInfo buttonResource in container.Resources)
            {
                if (!string.Equals(buttonResource.Tag?.Trim(), "BUTN", StringComparison.Ordinal))
                    continue;

                byte[] data = buttonResource.ModifiedData ?? gff.GetResourceData(buttonResource.Tag, buttonResource.Number);
                ButnResource button = ButnResource.Parse(data);
                if (button.IconId != (uint)icon.Number)
                    continue;

                button.Width += deltaWidth;
                button.Height += deltaHeight;
                buttonResource.ModifiedData = button.Build();
                buttonResource.IsModified = true;
                changed++;
            }

            foreach (ResourceInfo windowResource in container.Resources)
            {
                if (!string.Equals(windowResource.Tag?.Trim(), "WIND", StringComparison.Ordinal))
                    continue;

                byte[] data = windowResource.ModifiedData ?? gff.GetResourceData(windowResource.Tag, windowResource.Number);
                WindResource window = WindResource.Parse(data);
                bool windowChanged = false;

                foreach (WindItem item in window.Items)
                {
                    if (!item.IsButton)
                        continue;
                    ButnResource button = FindButton(container, gff, item.Id);
                    if (button == null || button.IconId != (uint)icon.Number)
                        continue;

                    item.X = checked((short)(item.X - deltaWidth / 2));
                    item.Y = checked((short)(item.Y - deltaHeight / 2));
                    windowChanged = true;
                }

                if (windowChanged)
                {
                    windowResource.ModifiedData = window.Build();
                    windowResource.IsModified = true;
                    changed++;
                }
            }

            return changed;
        }

        private static ButnResource FindButton(ResourceFile container, GffFile gff, uint number)
        {
            foreach (ResourceInfo resource in container.Resources)
            {
                if (!string.Equals(resource.Tag?.Trim(), "BUTN", StringComparison.Ordinal) ||
                    resource.Number != (int)number)
                {
                    continue;
                }

                return ButnResource.Parse(
                    resource.ModifiedData ?? gff.GetResourceData(resource.Tag, resource.Number));
            }

            return null;
        }
    }
}
