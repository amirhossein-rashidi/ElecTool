using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// ELECINSERT = درج یک نماد از کتابخانه در نقشه.
    /// ۱) شماره نماد از لیست  ۲) کلیک جای درج (نماد زنده با موس می‌آید)
    /// ۳) چرخش زنده با موس و کلیک دوم (Enter = بدون چرخش؛ A = تایپ زاویه به درجه)
    /// بعد از هر درج، همان نماد دوباره می‌آید تا Enter یا Esc بزنید.
    /// این فرمان سبک است: پشتیبان و ثبت ندارد و با U (Undo) خود AutoCAD برگردانده می‌شود.
    /// تعریف بلوک هرگز بازنویسی نمی‌شود؛ اگر نقشه بلوکی با همان نام دارد همان استفاده می‌شود.
    /// </summary>
    public static class SymbolInsertCommands
    {
        // آخرین نمادی که درج شد (پیش‌فرض دفعه بعد)
        private static string? _lastName;

        [Acrt.CommandMethod("ELECINSERT")]
        public static void InsertSymbol()
        {
            ElecRuntime.EnsureStarted();

            Document doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            Editor ed = doc.Editor;

            if (!ElecRuntime.Settings.Current.Enabled)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.disabled"));
                return;
            }

            string libPath = LibraryPath();
            if (!File.Exists(libPath))
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.noLibrary"));
                return;
            }

            List<string> names;
            try
            {
                names = ReadSymbolNames(libPath);
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECINSERT: cannot read library.", ex);
                OperationRunner.Say(ed, OperationRunner.T("lib.report.failed", ex.Message));
                return;
            }

            if (names.Count == 0)
            {
                OperationRunner.Say(ed, OperationRunner.T("lib.report.empty"));
                return;
            }

            // ۱) انتخاب نماد
            string? name = PickSymbol(ed, names);
            if (name == null)
            {
                OperationRunner.Say(ed, OperationRunner.T("op.cancelled"));
                return;
            }

            // ۲) آوردن تعریف بلوک به نقشه (اگر نبود)
            ObjectId blockId;
            try
            {
                blockId = EnsureBlock(doc, libPath, name);
            }
            catch (Exception ex)
            {
                ElecRuntime.Logger.Error("ELECINSERT: cannot import block.", ex);
                OperationRunner.Say(ed, OperationRunner.T("insert.failed", ex.Message));
                return;
            }

            if (blockId.IsNull)
            {
                OperationRunner.Say(ed, OperationRunner.T("insert.failed", name));
                return;
            }

            _lastName = name;

            // ۳) درج پشت‌سرهم تا Enter یا Esc
            int placed = 0;
            while (true)
            {
                BlockReference reference = new BlockReference(Point3d.Origin, blockId);
                bool keep = false;

                try
                {
                    var jig = new SymbolJig(reference, ed);

                    jig.Phase = 0;
                    PromptResult first = ed.Drag(jig);
                    if (first.Status != PromptStatus.OK)
                    {
                        break;
                    }

                    jig.Phase = 1;
                    PromptResult second = ed.Drag(jig);

                    if (second.Status == PromptStatus.Keyword)
                    {
                        var angleOptions = new PromptDoubleOptions("\n" + OperationRunner.T("insert.angleDegrees"));
                        angleOptions.DefaultValue = 0;
                        angleOptions.UseDefaultValue = true;
                        angleOptions.AllowNone = true;

                        PromptDoubleResult angleResult = ed.GetDouble(angleOptions);
                        if (angleResult.Status != PromptStatus.OK)
                        {
                            break;
                        }

                        jig.SetRotation(angleResult.Value * Math.PI / 180.0);
                    }
                    else if (second.Status == PromptStatus.None)
                    {
                        jig.SetRotation(0);
                    }
                    else if (second.Status != PromptStatus.OK)
                    {
                        break;
                    }

                    jig.ApplyFinal();

                    using (Transaction tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        var space = (BlockTableRecord)tr.GetObject(doc.Database.CurrentSpaceId, OpenMode.ForWrite);
                        space.AppendEntity(reference);
                        tr.AddNewlyCreatedDBObject(reference, true);
                        tr.Commit();
                    }

                    keep = true;
                    placed++;
                }
                catch (Exception ex)
                {
                    ElecRuntime.Logger.Error("ELECINSERT: placing failed.", ex);
                    OperationRunner.Say(ed, OperationRunner.T("insert.failed", ex.Message));
                    break;
                }
                finally
                {
                    if (!keep)
                    {
                        reference.Dispose();
                    }
                }
            }

            OperationRunner.Say(ed, OperationRunner.T(
                "insert.done",
                name,
                placed.ToString(CultureInfo.InvariantCulture)));
        }

        // ───────────────────────── انتخاب نماد ─────────────────────────

        private static string? PickSymbol(Editor ed, List<string> names)
        {
            OperationRunner.Say(ed, OperationRunner.T("insert.intro"));
            for (int i = 0; i < names.Count; i++)
            {
                OperationRunner.Say(ed, OperationRunner.T(
                    "insert.item",
                    (i + 1).ToString(CultureInfo.InvariantCulture),
                    names[i]));
            }

            var options = new PromptIntegerOptions("\n" + OperationRunner.T("insert.pickNumber"));
            options.LowerLimit = 1;
            options.UpperLimit = names.Count;

            int lastIndex = _lastName == null ? -1 : names.IndexOf(_lastName);
            if (lastIndex >= 0)
            {
                options.DefaultValue = lastIndex + 1;
                options.UseDefaultValue = true;
                options.AllowNone = true;
            }

            PromptIntegerResult result = ed.GetInteger(options);
            if (result.Status != PromptStatus.OK)
            {
                return null;
            }

            return names[result.Value - 1];
        }

        // ───────────────────────── آوردن بلوک به نقشه ─────────────────────────

        /// <summary>اگر بلوک در نقشه نیست، از کتابخانه می‌آورد؛ اگر هست همان را استفاده می‌کند.</summary>
        private static ObjectId EnsureBlock(Document doc, string libPath, string name)
        {
            Database target = doc.Database;

            using (Transaction tr = target.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(target.BlockTableId, OpenMode.ForRead);
                if (table.Has(name))
                {
                    ObjectId existing = table[name];
                    tr.Commit();
                    return existing;
                }

                tr.Commit();
            }

            using (Database lib = ReadLibrary(libPath))
            {
                var ids = new ObjectIdCollection();

                using (Transaction tr = lib.TransactionManager.StartTransaction())
                {
                    var table = (BlockTable)tr.GetObject(lib.BlockTableId, OpenMode.ForRead);
                    if (table.Has(name))
                    {
                        ids.Add(table[name]);
                    }

                    tr.Commit();
                }

                if (ids.Count == 0)
                {
                    return ObjectId.Null;
                }

                var map = new IdMapping();
                lib.WblockCloneObjects(ids, target.BlockTableId, map, DuplicateRecordCloning.Ignore, false);
            }

            using (Transaction tr = target.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(target.BlockTableId, OpenMode.ForRead);
                ObjectId result = table.Has(name) ? table[name] : ObjectId.Null;
                tr.Commit();
                return result;
            }
        }

        // ───────────────────────── کمکی‌ها ─────────────────────────

        private static List<string> ReadSymbolNames(string libPath)
        {
            var names = new List<string>();

            using (Database db = ReadLibrary(libPath))
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                var table = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                HashSet<string> internalBlocks = SymbolLibraryHelper.FindInternalBlocks(tr, table);

                foreach (ObjectId id in table)
                {
                    var btr = (BlockTableRecord)tr.GetObject(id, OpenMode.ForRead);

                    if (btr.IsLayout || btr.IsAnonymous ||
                        btr.IsFromExternalReference || btr.IsFromOverlayReference)
                    {
                        continue;
                    }

                    // بلوک داخلی (مثل L88) نماد مستقل نیست.
                    if (internalBlocks.Contains(btr.Name))
                    {
                        continue;
                    }

                    names.Add(btr.Name);
                }

                tr.Commit();
            }

            names.Sort(StringComparer.Ordinal);
            return names;
        }

        private static Database ReadLibrary(string path)
        {
            var db = new Database(false, true);
            db.ReadDwgFile(path, FileShare.ReadWrite, true, string.Empty);
            db.CloseInput(true);
            return db;
        }

        private static string LibraryPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ElecTool",
                "library",
                "symbols.dwg");
        }

        // ───────────────────────── نمایش زنده با موس ─────────────────────────

        /// <summary>
        /// فاز ۰: نماد دنبال موس می‌آید (انتخاب جای درج).
        /// فاز ۱: جای نماد ثابت است و با حرکت موس می‌چرخد (انتخاب زاویه).
        /// </summary>
        private sealed class SymbolJig : EntityJig
        {
            private readonly Editor _ed;
            private readonly BlockReference _reference;
            private Point3d _position = Point3d.Origin;
            private double _rotation;

            public SymbolJig(BlockReference reference, Editor ed)
                : base(reference)
            {
                _reference = reference;
                _ed = ed;
            }

            /// <summary>۰ = جای درج، ۱ = زاویه.</summary>
            public int Phase { get; set; }

            public void SetRotation(double radians)
            {
                _rotation = radians;
            }

            public void ApplyFinal()
            {
                _reference.Position = _position;
                _reference.Rotation = _rotation;
            }

            protected override SamplerStatus Sampler(JigPrompts prompts)
            {
                if (Phase == 0)
                {
                    var options = new JigPromptPointOptions("\n" + OperationRunner.T("insert.pickPoint"));
                    options.UserInputControls =
                        UserInputControls.Accept3dCoordinates |
                        UserInputControls.NullResponseAccepted;

                    PromptPointResult result = prompts.AcquirePoint(options);
                    if (result.Status == PromptStatus.Cancel)
                    {
                        return SamplerStatus.Cancel;
                    }

                    if (result.Status != PromptStatus.OK)
                    {
                        return SamplerStatus.OK;
                    }

                    Point3d point = result.Value.TransformBy(_ed.CurrentUserCoordinateSystem);
                    if (point.DistanceTo(_position) < 1e-9)
                    {
                        return SamplerStatus.NoChange;
                    }

                    _position = point;
                    return SamplerStatus.OK;
                }

                var angleOptions = new JigPromptPointOptions();
                angleOptions.SetMessageAndKeywords(
                    "\n" + OperationRunner.T("insert.pickAngle"),
                    "Angle");
                angleOptions.BasePoint = _position.TransformBy(_ed.CurrentUserCoordinateSystem.Inverse());
                angleOptions.UseBasePoint = true;
                angleOptions.UserInputControls =
                    UserInputControls.Accept3dCoordinates |
                    UserInputControls.NullResponseAccepted;

                PromptPointResult angleResult = prompts.AcquirePoint(angleOptions);
                if (angleResult.Status == PromptStatus.Cancel)
                {
                    return SamplerStatus.Cancel;
                }

                if (angleResult.Status != PromptStatus.OK)
                {
                    return SamplerStatus.OK;
                }

                Point3d end = angleResult.Value.TransformBy(_ed.CurrentUserCoordinateSystem);
                double dx = end.X - _position.X;
                double dy = end.Y - _position.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 1e-9)
                {
                    return SamplerStatus.NoChange;
                }

                double angle = Math.Atan2(dy, dx);
                if (Math.Abs(angle - _rotation) < 1e-9)
                {
                    return SamplerStatus.NoChange;
                }

                _rotation = angle;
                return SamplerStatus.OK;
            }

            protected override bool Update()
            {
                _reference.Position = _position;
                _reference.Rotation = _rotation;
                return true;
            }
        }
    }
}
