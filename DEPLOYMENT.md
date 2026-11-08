# دليل الإعداد والنشر التلقائي لدليل وسيرفر ROVEN (roven-eg.online)
## ROVEN VPS Deployment & CI/CD Administration Guide

يوثق هذا الدليل البنية التحتية وخطوات تشغيل ونشر متجر **ROVEN** على نفس السيرفر الحالي الخاص بمشروع **Valens**، مع ربط الدومين الجديد `roven-eg.online` وتأمين قاعدة البيانات والنسخ الاحتياطي التلقائي.

---

## 1. بنية المشاريع على السيرفر (Server Architecture)

السيرفر الحالي يحتوي بالفعل على بيئة عمل متكاملة:
- **مشروع Valens**: يعمل على المنفذ `5000` (API) والمنفذ `3000` (Frontend).
- **مشروع ROVEN**: تطبيق متكامل (Monolith MVC) يعمل على المنفذ **`5050`** (دون أي تعارض مع Valens نهائياً).
- **قاعدة البيانات (SQL Server 2022)**: حاوية Docker واحدة على المنفذ `1433` تدير قاعدتي بيانات معزولتين:
  - قاعدة `valenssupplements`
  - قاعدة **`rovendb`** (الخاصة بـ ROVEN)
- **خادم الويب العكسي (Nginx Reverse Proxy)**:
  - `valenssupplements.com` يوجه إلى 5000 / 3000
  - **`roven-eg.online`** و **`www.roven-eg.online`** يوجهان مباشرة إلى **`http://127.0.0.1:5050`**

---

## 2. المسار على السيرفر (VPS Directory Layout)

```
/var/www/roven-eg.online/
├── published/        # ملفات الـ DLL والتشغيل الجاهزة بعد dotnet publish
├── uploads/          # المجلد الدائم للصور المرفوعة (Banners & Products)
│   ├── banners/
│   └── products/
├── backups/          # النسخ الاحتياطية اليومية (قاعدة البيانات والصور)
│   ├── database/
│   └── uploads/
├── logs/             # سجلات النشر والـ Backup
│   ├── deploy/
│   └── backup/
├── scripts/          # سكربتات الأتمتة
│   ├── deploy.sh     # سكربت السحب والبناء وإعادة التشغيل (يستدعيه GitHub Actions)
│   ├── secure-env.sh # ضبط المتغيرات والتأكد من وجود قاعدة البيانات
│   ├── backup.sh     # النسخ الاحتياطي اليومي
│   └── restore.sh    # استرجاع الكوارث
├── systemd/
│   └── roven-web.service # إعداد خدمة النظام في لينكس
└── nginx/
    └── roven-eg.online.conf # ملف إعدادات Nginx
```

---

## 3. إجابة استفسار قاعدة البيانات (Database Handling)

> **هل الداتا بيز هتظبط ولا اعمل ايه؟**

**نعم، الداتا بيز هتظبط 100% تلقائياً دون الحاجة لأي سيرفر جديد!**
1. **نفس حاوية SQL Server 2022**: يمكن لمحرك SQL Server استضافة عشرات قواعد البيانات المستقلة تماماً.
2. **الإنشاء والترحيل التلقائي (Automatic EF Core Migration & Seeding)**:
   - تم تحديث [Program.cs](file:///d:/Roven-eg/roven-eg/Program.cs) ليقوم باستدعاء `await context.Database.MigrateAsync();` ثم `DbInitializer.SeedRolesAndAdminAsync()`.
   - هذا يعني أنه عند تشغيل التطبيق لأول مرة على السيرفر، سيقوم **Entity Framework Core** بإنشاء قاعدة البيانات `rovendb` تلقائياً، وتطبيق جميع الجداول والتحديثات (Migrations)، ثم إدخال الحساب الإداري الافتراضي، المحافظات، الأقسام، العطور، والعروض فوراً!
3. **سكربت secure-env.sh**:
   - يتحقق إضافياً عبر `sqlcmd` من وجود قاعدة `rovendb` وينشئها في حال لم تكن موجودة لمنع أي أخطاء في الصلاحيات.

---

## 4. ربط الدومين (DNS Configuration)

من لوحة تحكم موقع حجز الدومين (`roven-eg.online`):
أضف سجلات الـ DNS التالية مشيرة إلى عنوان الـ IP العام الخاص بالـ VPS:

| النوع (Type) | الاسم (Host / Name) | القيمة (Value / Points to) | TTL |
| :--- | :--- | :--- | :--- |
| **A Record** | `@` | `IP_الخاص_بالسيرفر` | Automatic / 1 Hour |
| **A Record** أو **CNAME** | `www` | `IP_الخاص_بالسيرفر` أو `roven-eg.online` | Automatic / 1 Hour |

---

## 5. تجهيز السيرفر لأول مرة (One-Time Initial Server Setup)

ادخل على السيرفر عبر SSH كـ `root`:

```bash
# 1. إنشاء مجلد المشروع واستنساخ الكود
sudo mkdir -p /var/www/roven-eg.online
sudo chown -R $USER:$USER /var/www/roven-eg.online
git clone <YOUR_ROVEN_GITHUB_REPO_URL> /var/www/roven-eg.online

# 2. منح صلاحيات التنفيذ للسكربتات
cd /var/www/roven-eg.online
chmod +x scripts/*.sh

# 3. تشغيل سكربت الإعداد الأمني الأولي لإنشاء roven.env وضبط الداتا بيز والخدمة
sudo ./scripts/secure-env.sh

# 4. تفعيل موقع Nginx
sudo cp nginx/roven-eg.online.conf /etc/nginx/sites-available/roven-eg.online
sudo ln -sf /etc/nginx/sites-available/roven-eg.online /etc/nginx/sites-enabled/roven-eg.online
sudo nginx -t && sudo systemctl reload nginx

# 5. استخراج شهادة الأمان المجانية SSL (HTTPS)
sudo certbot --nginx -d roven-eg.online -d www.roven-eg.online

# 6. تشغيل أول عملية نشر يدوياً للتأكد من كل شيء
./scripts/deploy.sh
```

---

## 6. إعداد النشر التلقائي عبر GitHub Actions

تم إنشاء ملف Workflow في المسار:
[`.github/workflows/deploy.yml`](file:///d:/Roven-eg/roven-eg/.github/workflows/deploy.yml)

بمجرد عمل Push على فرع `Production`، سيتصل GitHub بالسيرفر وينفذ `deploy.sh` تلقائياً!

### أسرار المستودع المطلوبة (GitHub Repository Secrets):
توجه في GitHub إلى: **Settings -> Secrets and variables -> Actions -> New repository secret**
وأضف نفس أسرار سيرفر Valens:

1. `VPS_HOST`: عنوان الـ IP الخاص بالسيرفر.
2. `VPS_USER`: اسم المستخدم (غالباً `root` أو المستخدم الذي تملكه).
3. `VPS_SSH_KEY`: المفتاح الخاص (Private Key) الخاص بالـ SSH.
4. `VPS_PORT`: المنفذ الخاص بالـ SSH (افتراضياً `22`).

---

## 7. أتمتة النسخ الاحتياطي اليومي (Daily Automated Backup)

لجدولة نسخ احتياطي لقاعدة البيانات والصور يومياً الساعة 3:00 فجراً:

```bash
sudo crontab -e
```

أضف السطر التالي في نهاية الملف:
```cron
0 3 * * * /bin/bash /var/www/roven-eg.online/scripts/backup.sh > /dev/null 2>&1
```

---

## 8. استرجاع النسخ الاحتياطية في حال الطوارئ (Disaster Recovery)

في حال حدوث أي خطأ، يمكنك استرجاع أي نقطة زمنية بكتابة أمر واحد:

```bash
sudo /var/www/roven-eg.online/scripts/restore.sh \
  /var/www/roven-eg.online/backups/database/rovendb_backup_20261108_030000.bak \
  /var/www/roven-eg.online/backups/uploads/uploads_backup_20261108_030000.tar.gz
```

---

## 9. أوامر المتابعة ومراقبة التطبيق (Monitoring)

- **متابعة السجلات الحية للتطبيق**:
  ```bash
  journalctl -u roven-web -f
  ```
- **فحص حالة الخدمة**:
  ```bash
  sudo systemctl status roven-web
  ```
- **إعادة تشغيل التطبيق يدوياً**:
  ```bash
  sudo systemctl restart roven-web
  ```
- **سجلات النشر**:
  ```bash
  cat /var/www/roven-eg.online/logs/deploy/deploy_*.log
  ```

