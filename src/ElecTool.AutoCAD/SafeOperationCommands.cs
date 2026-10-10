using System;
using System.Collections.Generic;
using System.Globalization;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using AcCore = Autodesk.AutoCAD.ApplicationServices.Core.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// فرمان‌های آزمایشی زیرساخت امن:
    /// ELECTEST = ساخت چند شیء آزمایشی از طریق OperationRunner
    /// ELECUNDO = پاک کردن فقط اشیای ثبت‌شده در آخرین عملیات (از طریق OperationRunner)
    /// ELECBLOCKS = گزارش فقط‌خواندنی بلوک‌های نقشه (از طریق BlockInventory)
    /// تمام منطق امن (پشتیبان، ثبت، پیش‌نمایش، تأیید، بازگردانی) در OperationRunner است.
    /// ELECPICK = بررسی فقط‌خواندنی یک نماد و ثبت نام آن (از طریق SymbolInspector)
    /// نکته: ELECBLOCKS و ELECPICK موقتاً اینجا هستند چون این کلاس در Plugin.cs ثبت شده؛ بعداً به کلاس خودش منتقل می‌شود.
    /// </summary>
    public static class SafeOperationCommands
    {
        private const string TestOperationName = "ELECTEST";

        [Acrt.CommandMethod("ELECTEST")]
        public static void RunTestOperation()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            // اگر ابزار غیرفعال است یا نقشه ذخیره نیست، قبل از گرفتن نقطه متوقف شو.
            if (!OperationRunner.IsReady(doc))
            {
                return;
            }

            var pointOptions = new PromptPointOptions("\n" + OperationRunner.T("op.test.pickPoint"));
            PromptPointResult pointResult = ed.GetPoint(pointOptions);
            if (pointResult.Status != PromptStatus.OK)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            Point3d center = pointResult.Value.TransformBy(ed.CurrentUserCoordinateSystem);
            double viewSize = Convert.ToDouble(AcCore.GetSystemVariable("VIEWSIZE"), CultureInfo.InvariantCulture);
            double size = viewSize / 20.0;
            if (size <= 0)
            {
                size = 100.0;
            }

            var operation = new SafeOperation(
                TestOperationName,
                OperationRunner.T("op.test.confirm"),
                OperationRunner.T("op.test.done"),
                () => BuildTestEntities(center, size),
                () => BuildTestEntities(center, size));

            OperationRunner.Run(doc, operation);
        }

        [Acrt.CommandMethod("ELECUNDO")]
        public static void UndoLastOperation()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            OperationRunner.UndoLast(doc);
        }

        [Acrt.CommandMethod("ELECBLOCKS")]
        public static void ListBlocks()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            BlockInventory.Run(doc);
        }

        [Acrt.CommandMethod("ELECPICK")]
        public static void InspectSymbol()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            SymbolInspector.Run(doc);
        }

        private static List<Entity> BuildTestEntities(Point3d center, double size)
        {
            double x = center.X;
            double y = center.Y;
            double z = center.Z;

            var list = new List<Entity>();
            list.Add(new Line(new Point3d(x - size, y, z), new Point3d(x + size, y, z)));
            list.Add(new Line(new Point3d(x, y - size, z), new Point3d(x, y + size, z)));
            list.Add(new Circle(center, Vector3d.ZAxis, size / 2.0));
            return list;
        }
    }
}
