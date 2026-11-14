using Acrt = Autodesk.AutoCAD.Runtime;

namespace ElecTool.AutoCAD
{
    /// <summary>
    /// میانبر درج مستقیم هر نماد (بدون لیست)؛ مثل ELECSOC برای پریز.
    /// همه از همان ELECINSERT استفاده می‌کنند (جدول ۴۲ ردیفی هندآف).
    /// اسم‌ها باید دقیقاً با اسم نماد در کتابخانه (ELECLIB) یکی باشد.
    /// </summary>
    public static class SymbolShortcutCommands
    {
        [Acrt.CommandMethod("ELECSOC")]
        public static void InsertSoc()
        {
            SymbolInsertCommands.InsertByName("پریز");
        }

        [Acrt.CommandMethod("ELECTELS")]
        public static void InsertTels()
        {
            SymbolInsertCommands.InsertByName("پریز تلفن");
        }

        [Acrt.CommandMethod("ELECANTS")]
        public static void InsertAnts()
        {
            SymbolInsertCommands.InsertByName("پریز آنتن");
        }

        [Acrt.CommandMethod("ELECANTN")]
        public static void InsertAntn()
        {
            SymbolInsertCommands.InsertByName("توضیحات کابل آنتن");
        }

        [Acrt.CommandMethod("ELECTELN")]
        public static void InsertTeln()
        {
            SymbolInsertCommands.InsertByName("توضیحات پریز تلفن");
        }

        [Acrt.CommandMethod("ELECTELW")]
        public static void InsertTelw()
        {
            SymbolInsertCommands.InsertByName("جزئیات سیم تلفن");
        }

        [Acrt.CommandMethod("ELECTELB")]
        public static void InsertTelb()
        {
            SymbolInsertCommands.InsertByName("جعبه اصلی تلفن");
        }

        [Acrt.CommandMethod("ELECTELF")]
        public static void InsertTelf()
        {
            SymbolInsertCommands.InsertByName("تابلو تلفن طبقات");
        }

        [Acrt.CommandMethod("ELECANTF")]
        public static void InsertAntf()
        {
            SymbolInsertCommands.InsertByName("تابلو آنتن طبقات");
        }

        [Acrt.CommandMethod("ELECSW1")]
        public static void InsertSw1()
        {
            SymbolInsertCommands.InsertByName("کلید یک پل");
        }

        [Acrt.CommandMethod("ELECSW2")]
        public static void InsertSw2()
        {
            SymbolInsertCommands.InsertByName("کلید دو پل");
        }

        [Acrt.CommandMethod("ELECSWC")]
        public static void InsertSwc()
        {
            SymbolInsertCommands.InsertByName("کلید تبدیل");
        }

        [Acrt.CommandMethod("ELECSWX")]
        public static void InsertSwx()
        {
            SymbolInsertCommands.InsertByName("کلید صلیبی");
        }

        [Acrt.CommandMethod("ELECSWT")]
        public static void InsertSwt()
        {
            SymbolInsertCommands.InsertByName("شصتی چراغ");
        }

        [Acrt.CommandMethod("ELECSWAC")]
        public static void InsertSwac()
        {
            SymbolInsertCommands.InsertByName("کلید کولر");
        }

        [Acrt.CommandMethod("ELECLMP")]
        public static void InsertLmp()
        {
            SymbolInsertCommands.InsertByName("چراغ سقفی");
        }

        [Acrt.CommandMethod("ELECEYE1")]
        public static void InsertEye1()
        {
            SymbolInsertCommands.InsertByName("چراغ چشمی با یک لامپ");
        }

        [Acrt.CommandMethod("ELECEYE2")]
        public static void InsertEye2()
        {
            SymbolInsertCommands.InsertByName("چراغ چشمی با دو لامپ");
        }

        [Acrt.CommandMethod("ELECWAL")]
        public static void InsertWal()
        {
            SymbolInsertCommands.InsertByName("چراغ دیواری");
        }

        [Acrt.CommandMethod("ELECWALM")]
        public static void InsertWalm()
        {
            SymbolInsertCommands.InsertByName("چراغ دیواری با حباب شیری");
        }

        [Acrt.CommandMethod("ELECCHD")]
        public static void InsertChd()
        {
            SymbolInsertCommands.InsertByName("لوستر");
        }

        [Acrt.CommandMethod("ELECCHDK")]
        public static void InsertChdk()
        {
            SymbolInsertCommands.InsertByName("لوستر آشپزخانه");
        }

        [Acrt.CommandMethod("ELECHAL")]
        public static void InsertHal()
        {
            SymbolInsertCommands.InsertByName("چراغ هالوژنه سقفی");
        }

        [Acrt.CommandMethod("ELECPNL")]
        public static void InsertPnl()
        {
            SymbolInsertCommands.InsertByName("پنل سقفی توکار 60x60");
        }

        [Acrt.CommandMethod("ELECDETS")]
        public static void InsertDets()
        {
            SymbolInsertCommands.InsertByName("دتکتور دودی");
        }

        [Acrt.CommandMethod("ELECDETHI")]
        public static void InsertDethi()
        {
            SymbolInsertCommands.InsertByName("دتکتور حرارتی افزایشی");
        }

        [Acrt.CommandMethod("ELECDETHF")]
        public static void InsertDethf()
        {
            SymbolInsertCommands.InsertByName("دتکتور حرارتی دما ثابت");
        }

        [Acrt.CommandMethod("ELECFIRT")]
        public static void InsertFirt()
        {
            SymbolInsertCommands.InsertByName("شصتی اعلان حریق");
        }

        [Acrt.CommandMethod("ELECFIRZ")]
        public static void InsertFirz()
        {
            SymbolInsertCommands.InsertByName("زون بندی اعلان حریق");
        }

        [Acrt.CommandMethod("ELECFIRS")]
        public static void InsertFirs()
        {
            SymbolInsertCommands.InsertByName("آژیر اعلان حریق");
        }

        [Acrt.CommandMethod("ELECFIRP")]
        public static void InsertFirp()
        {
            SymbolInsertCommands.InsertByName("پنل اعلان حریق");
        }

        [Acrt.CommandMethod("ELECFIRN")]
        public static void InsertFirn()
        {
            SymbolInsertCommands.InsertByName("توضیحات کابل اعلان حریق");
        }

        [Acrt.CommandMethod("ELECBEL")]
        public static void InsertBel()
        {
            SymbolInsertCommands.InsertByName("زنگ");
        }

        [Acrt.CommandMethod("ELECBELT")]
        public static void InsertBelt()
        {
            SymbolInsertCommands.InsertByName("شصتی زنگ");
        }

        [Acrt.CommandMethod("ELECINT")]
        public static void InsertInt()
        {
            SymbolInsertCommands.InsertByName("پنل آیفون");
        }

        [Acrt.CommandMethod("ELECHOOD")]
        public static void InsertHood()
        {
            SymbolInsertCommands.InsertByName("هود");
        }

        [Acrt.CommandMethod("ELECFAN")]
        public static void InsertFan()
        {
            SymbolInsertCommands.InsertByName("فن 50 وات");
        }

        [Acrt.CommandMethod("ELECMDB")]
        public static void InsertMdb()
        {
            SymbolInsertCommands.InsertByName("تابلو اصلی برق");
        }

        [Acrt.CommandMethod("ELECERT")]
        public static void InsertErt()
        {
            SymbolInsertCommands.InsertByName("چاه ارت");
        }

        [Acrt.CommandMethod("ELECERTP")]
        public static void InsertErtp()
        {
            SymbolInsertCommands.InsertByName("چاه ارت پلان همبندی");
        }

        [Acrt.CommandMethod("ELECBUSN")]
        public static void InsertBusn()
        {
            SymbolInsertCommands.InsertByName("توضیحات اتصال شین اصلی");
        }

        [Acrt.CommandMethod("ELECREB")]
        public static void InsertReb()
        {
            SymbolInsertCommands.InsertByName("میلگرد ده میلی متری");
        }
    }
}
