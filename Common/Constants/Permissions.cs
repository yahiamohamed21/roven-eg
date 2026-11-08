namespace Stockify.Common.Constants;

public static class Permissions
{
    // Permission Keys
    public const string Dashboard = "Dashboard";
    public const string Products = "Products";
    public const string Categories = "Categories";
    public const string Inventory = "Inventory";
    public const string POS = "POS";
    public const string Orders = "Orders";
    public const string Customers = "Customers";
    public const string Coupons = "Coupons";
    public const string HomeControl = "HomeControl";
    public const string Messages = "Messages";
    public const string Reports = "Reports";
    public const string Users = "Users";

    public record PermissionItem(
        string Key,
        string NameAr,
        string NameEn,
        string Description,
        string Icon,
        string Category
    );

    public static readonly List<PermissionItem> All = new()
    {
        // 1. الإدارة والتحليلات
        new(Dashboard, "لوحة التحكم والمؤشرات", "Dashboard & KPIs", "الاطلاع على الإحصائيات العامة ومعدلات المبيعات ونشاط المتجر", "fa-solid fa-chart-pie", "الإدارة والتحليلات"),
        new(Reports, "التقارير المحاسبية والمالية", "Financial Reports", "استعراض وتصدير تقارير الأرباح والمبيعات والمخزون بصيغ Excel", "fa-solid fa-chart-column", "الإدارة والتحليلات"),
        new(Users, "إدارة المستخدمين والصلاحيات", "Users & Access Control", "إنشاء وتعديل حسابات الموظفين وتعيين الصلاحيات وتغيير كلمات المرور", "fa-solid fa-shield-halved", "الإدارة والتحليلات"),

        // 2. الكتالوج والمستودعات
        new(Products, "كتالوج المنتجات والعطور", "Products Catalog", "إضافة وتعديل بيانات العطور، التراكيز، الأسعار، وحالات العرض", "fa-solid fa-spray-can-sparkles", "الكتالوج والمستودعات"),
        new(Categories, "تصنيفات وعائلات العطور", "Categories", "إدارة أقسام وتصنيفات العطور (Extrait, EDP, Discovery)", "fa-solid fa-layer-group", "الكتالوج والمستودعات"),
        new(Inventory, "المستودع وحركة المخزون", "Inventory Control", "متابعة أرصدة المخزون، تسجيل حركات الصرف والتوريد، وتنبيهات النواقص", "fa-solid fa-boxes-stacked", "الكتالوج والمستودعات"),

        // 3. المبيعات ونقاط البيع
        new(POS, "نقطة البيع والإنشاء المباشر (POS)", "Create Order (POS)", "إنشاء طلبات بيع مباشرة للعملاء داخل البوتيك وتطبيق الخصومات الفورية", "fa-solid fa-cash-register", "المبيعات والعملاء"),
        new(Orders, "سجل ومتابعة الطلبات", "Orders Management", "استعراض ومتابعة كافة الطلبات الصادرة والواردة وتحديث حالات الشحن", "fa-solid fa-receipt", "المبيعات والعملاء"),
        new(Customers, "قاعدة بيانات العملاء (CRM)", "Customers CRM", "إدارة سجلات العملاء وعناوينهم وأرقام هواتفهم وسجل مشترياتهم", "fa-solid fa-users", "المبيعات والعملاء"),
        new(Coupons, "الكوبونات وقسائم الخصم", "Coupons & Discounts", "إنشاء كوبونات التخفيض ومتابعة مبيعات وأداء كل كوبون", "fa-solid fa-ticket", "المبيعات والعملاء"),

        // 4. واجهة المتجر الإلكتروني
        new(HomeControl, "التحكم بالصفحة الرئيسية", "Home Control", "تخصيص بنرات العرض وقسم المميزات وروابط منصات التواصل الاجتماعي", "fa-solid fa-sliders", "واجهة المتجر الإلكتروني"),
        new(Messages, "رسائل واستفسارات المتجر", "Store Inquiries", "استقبال وإدارة رسائل العملاء الواردة من صفحة تواصل معنا (Contact Us)", "fa-solid fa-envelope-open-text", "واجهة المتجر الإلكتروني")
    };

    // Default permissions for Sales Rep / POS Operator
    public static readonly string[] DefaultSales =
    {
        Dashboard,
        POS,
        Orders,
        Customers,
        Products
    };

    // Default permissions for Administrator / Manager
    public static readonly string[] DefaultAdmin =
    {
        Dashboard,
        Products,
        Categories,
        Inventory,
        POS,
        Orders,
        Customers,
        Coupons,
        HomeControl,
        Messages,
        Reports,
        Users
    };
}

