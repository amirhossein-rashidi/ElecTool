using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// کمکی مشترک کتابخانه نمادها.
    /// «بلوک داخلی» = بلوکی که داخل بلوک دیگری از کتابخانه استفاده شده (مثل L88 داخل چراغ دیواری).
    /// بلوک داخلی نماد مستقل نیست و از لیست‌های ELECLIB و ELECLIBTEST پنهان می‌شود.
    /// </summary>
    internal static class SymbolLibraryHelper
    {
        /// <summary>نام بلوک‌هایی که داخل یک بلوک دیگر کتابخانه استفاده شده‌اند.</summary>
        public static HashSet<string> FindInternalBlocks(Transaction tr, BlockTable table)
        {
            var internalNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (ObjectId id in table)
            {
                var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);

                if (btr.IsLayout || btr.IsAnonymous ||
                    btr.IsFromExternalReference || btr.IsFromOverlayReference)
                {
                    continue;
                }

                foreach (ObjectId entityId in btr)
                {
                    var reference = tr.GetObject(entityId, OpenMode.ForRead) as BlockReference;
                    if (reference == null)
                    {
                        continue;
                    }

                    var target = (BlockTableRecord)tr.GetObject(reference.BlockTableRecord, OpenMode.ForRead);
                    if (!string.Equals(target.Name, btr.Name, StringComparison.Ordinal))
                    {
                        internalNames.Add(target.Name);
                    }
                }
            }

            return internalNames;
        }
    }
}
