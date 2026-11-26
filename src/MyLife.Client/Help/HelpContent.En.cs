using MudBlazor;

namespace MyLife.Client.Help;

/// <summary>Tutorial und Anleitung auf Englisch (britisches Englisch, wie die übrige Oberfläche).</summary>
public static partial class HelpContent
{
    private static HelpTexts English() => new(EnglishTutorial(), EnglishGuide());

    private static TutorialChapter[] EnglishTutorial() =>
    [
        new("willkommen", "myLife in five minutes", Icons.Material.Outlined.RocketLaunch,
            """
            myLife is your family's digital emergency folder. In one place you collect what your relatives need to know if something happens to you: accounts, insurance, contracts, certificates, digital accounts, contacts and personal wishes. **As long as you are well, nobody but you can read the data**, not even whoever runs the server.
            """,
            """
            **How it works**

            1. You set up your account and get your own encrypted **vault**.
            2. You fill it step by step: per area, the entries with their data sheets and documents.
            3. You invite your family and decide who may see which areas in an emergency.
            4. If you do not check in for a long time, or a trusted person requests access and you do not object, myLife releases exactly what you intended for that case.

            **The sidebar**

            | Menu item | What for |
            | --- | --- |
            | Overview | All areas with count, completeness and “last reviewed”, plus what expires soon |
            | Search | Full-text search across your entries |
            | Areas | Your entries, from “About me” to the checklist |
            | Family | Everyone on this instance, invitations |
            | Trusted persons | Who may see what in an emergency |
            | Shared with me | What others already share with you today |
            | Emergency access | Life sign, deadlines, requests |
            | Reading mode | What others have left you for an emergency |
            | Export & print | Emergency folder as PDF, backup, emergency package |
            | Administration | Activity log and trash; for administrators also settings and users |
            | Tutorial, Guide | This help |
            | Account | Reminders, security, details, sign out |

            > Encryption and decryption happen only in your browser. The server stores nothing but data it cannot read itself.
            """,
            ["anmelden", "gehoert-nicht-hinein"]),

        new("einrichten", "Setting up", Icons.Material.Outlined.Key,
            """
            How you get your account depends on whether myLife is already running: the **first person** sets up the instance with the setup wizard and becomes administrator. **Everyone else** receives an invitation.
            """,
            """
            **What to have ready**

            - about ten minutes,
            - a printer, or pen and paper, for the **recovery kit**,
            - an **authenticator app** on your phone, for example Aegis, Google or Microsoft Authenticator, or your password manager.

            **The first person (administrator)**

            1. The instance is installed (see the guide, part “Administration”). On the first visit the **setup wizard** starts.
            2. Enter the **setup code** from the container log.
            3. Enter your name and email address, choose a **master password**, print the **recovery kit** and type in a few words to check, then set up the authenticator app.
            4. **Create vault**: your browser generates the keys, which takes a few seconds.

            **Everyone else (invitation)**

            1. Open the link from the invitation email or from the message the inviting person sent you.
            2. Choose a master password, print the recovery kit, set up the authenticator app, done.

            > Nobody can reset the master password, not even the administrator. If it is lost, only the recovery kit helps. Keep it apart from the password, for example in a safe deposit box.
            """,
            ["einrichtungsassistent", "einladung-annehmen", "installation"]),

        new("anmelden", "Signing in and staying safe", Icons.Material.Outlined.Login,
            """
            To sign in you need three things: your **email address**, your **master password** and a **second factor**, that is the code from your authenticator app or a passkey.
            """,
            """
            **Day to day**

            - After a while without activity myLife signs you out and removes all keys from memory. The administrator decides how long; 15 minutes is typical.
            - If you reload the page you stay signed in, but the vault is **locked**. Your master password is enough to unlock it.
            - Under **Account → Security** you change your master password and set up **passkeys**, such as fingerprint or face recognition instead of the code.

            **Forgot your password?**

            On the sign-in page choose **Forgot password?** and enter the 24 words from your recovery kit. You then choose a new master password; your vault stays unchanged.
            """,
            ["anmelden", "entsperren", "passwort-vergessen", "passkeys"]),

        new("erfassen", "Your first entries", Icons.Material.Outlined.Inventory2,
            """
            Your vault is divided into **areas**, from “About me” through bank accounts and insurance to your digital legacy. In each area you create **entries**. Every entry has a data sheet with suitable fields and as many **documents** as you like.
            """,
            """
            **An example: your current account**

            1. In the sidebar under **Areas**, click **Bank accounts & securities**.
            2. Choose **New Bank account**. The small arrow next to it offers the other types of the area, such as securities account or safe deposit box.
            3. Fill in bank, IBAN, account holder and powers of attorney. Required fields are marked; the IBAN check digits are verified.
            4. Under **Wish for your survivors**, decide what should happen to the account, for example “close” or “keep running”.
            5. **Save**. Then, under **Documents**, add the account agreement as a PDF or photo.

            **A good order to start with**

            - **Account → Details**: surname, first name and a picture.
            - **About me**: personal details and ID documents.
            - **Certificates & legal documents**: will, powers of attorney, living will, each with **where the original is kept**.
            - **Insurance**: above all life and accident insurance; they have short notification periods after a death.
            - **Password manager**: which one it is and how its emergency access works.

            **What does not belong in it**

            PINs, TANs, full card numbers, individual passwords and seed phrases. myLife warns you when you save a text that looks like one of these.
            """,
            ["eintrag-anlegen", "dokumente", "wuensche-verweise", "gehoert-nicht-hinein", "eigenes-datenblatt"]),

        new("ueberblick", "Keeping track", Icons.Material.Outlined.Dashboard,
            """
            An emergency folder only helps if it is up to date. The **overview** therefore shows how complete each area is and when you last reviewed it, and myLife **reminds** you by email.
            """,
            """
            **On the overview**

            - Each tile shows the number of entries, how complete the area is and when it was last reviewed.
            - Once you have gone through an area, click **Mark as reviewed**.
            - At the top you see what **expires soon** (ID card, payment card, contract) and which **follow-ups** are due.

            **Setting up reminders**

            Under **Account → Reminders** you decide how often you want to review the areas (default: every 6 months) and how many days before an expiry date myLife reminds you (default: 60 days). The emails contain no details; you see what is due after signing in.

            **Finding things**

            **Search** looks through all your entries. It runs only in your browser and does not search the content of PDFs.
            """,
            ["uebersicht", "erinnerungen", "wiedervorlage", "suche"]),

        new("familie", "Inviting your family", Icons.Material.Outlined.Diversity3,
            """
            Trusted persons always have their own account on this instance, so you invite them first. Everyone you invite gets **their own vault** and sees nothing of yours until you release something.
            """,
            """
            **Inviting someone**

            1. Open **Family** in the sidebar and choose **Invite someone**.
            2. Enter the email address and the language of the invitation.
            3. Decide: **Send by email from myLife** (convenient, the usual way) or **Share the link yourself**, for example via messenger (safest, the server never sees the link).
            4. The person accepts the invitation and sets up their account.

            **Checking keys without a phone call**

            When the person accepts, your browser checks their key automatically and remembers it. **Family** then shows “Confirmed by you”. People invited by someone else count as confirmed once someone you trust has countersigned their key.

            > If you ever see **Key changed!**, myLife releases nothing to that person. Talk to them through another channel and contact the administrator.
            """,
            ["einladen", "schluessel"]),

        new("vertrauenspersonen", "Trusted persons and releases", Icons.Material.Outlined.VolunteerActivism,
            """
            Here you decide **who may see which areas in an emergency**. There are two cases, released separately: **deceased** and **incapacitated**. In addition you can **share** individual areas **now**, read-only.
            """,
            """
            **Appointing a trusted person**

            1. Open **Trusted persons** and choose **Add trusted person**.
            2. Select the person and enter the **relationship**, for example daughter or brother. Relationship and note are encrypted; the server cannot see them.
            3. Decide whether they **may request access**.
            4. In the table, tick per area: **Deceased**, **Incapacitated** and, if you like, **Share now**. **all** ticks a whole column.
            5. Below, select the **personal letters** the person should receive.
            6. **Save and encrypt**. Your browser encrypts the area keys for this person; the server only hands them out when the case occurs.

            **Example**

            | Area | Partner | Son |
            | --- | --- | --- |
            | Bank accounts & securities | deceased, incapacitated | deceased |
            | Insurance | deceased, incapacitated | deceased |
            | Personal wishes | deceased | deceased |
            | Checklist | deceased, incapacitated | deceased |

            > Release the **checklist** like any other area, otherwise your trusted persons will not see it in reading mode.
            """,
            ["vertrauenspersonen", "briefe", "sofort-teilen"]),

        new("checkliste", "The checklist", Icons.Material.Outlined.Checklist,
            """
            The **checklist** guides your trusted persons step by step through what needs doing: in the first 24 hours, the first week, the first month and later. Every task shows the matching entries, for example all contracts that should be cancelled.
            """,
            """
            **Starting from the template**

            1. In the sidebar under **Areas**, open the **Checklist**.
            2. Choose **Use template**. The tasks of the template for Germany land in your vault, from the doctor to the tax office.
            3. Change, add or delete tasks like any other entry. **New task** creates your own.

            **Linking tasks to entries**

            A task can list all entries of an area, optionally only those with a particular **wish**. Example: “Contracts & subscriptions” with the wish “cancel” lists exactly the contracts that should be cancelled. So keep the wish for your survivors up to date on your entries.

            **In an emergency**

            Your trusted persons tick off the tasks together in reading mode; everyone sees the same state and who did what.
            """,
            ["checkliste", "wuensche-verweise"]),

        new("notfallzugang", "Emergency access", Icons.Material.Outlined.HealthAndSafety,
            """
            Emergency access makes sure your trusted persons get to the data in an emergency, **but not before**. It becomes active as soon as you have appointed at least one trusted person.
            """,
            """
            **Route 1: no life sign**

            1. Every sign-in counts as a **life sign**. Alternatively, click the link in the reminder email or choose **Confirm I am well** under **Emergency access**.
            2. If there is no life sign for longer than your interval (default: 30 days), staged reminders follow, usually after 3, 7 and 14 days.
            3. If you react to none of them, the **waiting period** begins (default: 14 days).
            4. After that both cases are released, deceased and incapacitated.

            **Route 2: a trusted person's request**

            1. A trusted person with the right to request asks for access for one case.
            2. You immediately get an email with an **object** link, and all other trusted persons are informed.
            3. Clicking the link pauses the request. Then you sign in and decide: **Object for good** or **Let it continue**.
            4. Without an objection, the requested case is released after the request period (default: 7 days).

            **Your deadlines**

            Under **Emergency access → Your deadlines** you choose interval, waiting period and request period within the limits set by the administrator. A second email address and an ntfy topic for push messages are possible.

            > If sending email fails, no deadlines run on. A mail outage never leads to a release.

            **Back again?**

            If access was released although you are well, sign in and choose **Revoke release**. Your browser then creates new keys for everything affected.
            """,
            ["lebenszeichen", "fristen", "antrag-widersprechen", "freigabe-zuruecknehmen"]),

        new("ernstfall", "As a trusted person in an emergency", Icons.Material.Outlined.MenuBook,
            """
            If someone has appointed you as a trusted person, in an emergency you will find everything in **reading mode**: calm, step by step, with larger text if you wish and following the checklist.
            """,
            """
            **Requesting access**

            1. Open **Emergency access**. **For others** lists everyone who has appointed you.
            2. For the person, choose **Request access** and state the case: deceased or incapacitated.
            3. The person and their other trusted persons get an email straight away. If nobody objects, access is released after the request period and you get an email.

            **Reading**

            1. Open **Reading mode** and choose the person.
            2. Use **Next** and **Back** to go step by step through the checklist and the released areas. **Larger text** helps with reading.
            3. Tick off tasks once they are done; the other trusted persons see this.
            4. **Letters to you** come at the end.

            **When everything is done**

            All trusted persons for the case “deceased” can **delete the vault together for good**. Deletion happens seven days after the last agreement; until then anyone can withdraw their agreement.

            > What someone already shares with you today is under **Shared with me**, even without an emergency.
            """,
            ["zugang-beantragen", "lesemodus", "gemeinsam-loeschen", "mit-mir-geteilt"]),

        new("papier", "On paper and offline", Icons.Material.Outlined.IosShare,
            """
            myLife never locks your data in. Under **Export & print** you create an **emergency folder** to print, a **backup** in an open format and an **emergency package** that works without the server and without the internet.
            """,
            """
            **What is available**

            | Export | What for | Protection |
            | --- | --- | --- |
            | Emergency folder (PDF) | All or selected areas to print or keep | PDF password (AES-256) |
            | Print view | Paper folder, safe deposit box | none, plain text |
            | Backup (JSON) | All entries and documents, open format | your master password or recovery kit |
            | Emergency package | One HTML file for your family, for example on a USB stick | printed key sheet with 24 words |

            **The emergency package**

            1. Under **Export & print**, choose **Create emergency package**.
            2. Select areas, optionally with documents, and choose **Create and save package**.
            3. **Print the key sheet** right away: you see the 24 words only this once.
            4. Give the file to your family and keep the sheet separately, for example in a safe deposit box or with a notary.

            > Every printout and every export without a password contains your data in plain text. Keep it as safe as the originals.
            """,
            ["notfallordner", "datensicherung", "notfallpaket", "mylife-decrypt"]),

        new(AdministrationChapter, "For administrators", Icons.Material.Outlined.AdminPanelSettings,
            """
            Administrators look after the instance: email, deadlines, backup and users. **They still cannot look into anyone else's vault**, and they cannot trigger a release or shorten a user's deadlines.
            """,
            """
            **After setup**

            1. **Administration → Settings → General**: enter the **address of this instance**, for example `https://mylife.example.org`. It appears in the links in emails.
            2. Tab **Email**: enter the SMTP server and choose **Save and send test email**. Without email no emergency access deadlines run and no reminders are sent.
            3. Tab **Backup**: check that the daily backup is set up.
            4. Appoint a **second administrator**. The trusted persons cannot jointly delete the vault of the only administrator.

            **Regularly, about once a month**

            - The last backup is from today or yesterday, without errors.
            - The email queue is empty.
            - Install updates, choosing **Back up now** first.
            """,
            ["mailversand", "fristen-grenzen", "sicherung", "benutzer", "update"]),
    ];

    private static GuidePart[] EnglishGuide() =>
    [
        new("einrichten", "Setting up",
            "From the first visit to your own account.",
        [
            new("einrichtungsassistent", "Running the setup wizard", Icons.Material.Outlined.Key,
                "The first person on a new instance",
                "As long as there is no account yet, opening myLife starts the **setup wizard**. The first account becomes administrator and gets its own vault. All keys are created in the browser.",
                [
                    "Open myLife in the browser. The wizard starts with **Welcome**.",
                    "Enter the **setup code**. It is in the container log, for example in Container Manager under `mylife-app` → **Log**, on the line with the setup code for the wizard.",
                    "**Your account**: display name and email address. The address is used for signing in, reminders and emergency access.",
                    "Read and confirm **What does not belong in it**.",
                    "Choose a **master password**, repeat it and confirm that nobody can reset it. A sentence of several words is easy to remember and secure.",
                    "**Recovery kit**: **Print kit** or copy down the 24 words, then type in the requested words to check.",
                    "**Second factor**: scan the QR code with the authenticator app and enter the 6-digit code.",
                    "**Create vault**. From then on you sign in with email address, master password and code.",
                ],
                [
                    "Every restart of the container produces a new setup code. Once the first account exists there is no code any more and the wizard is locked.",
                    "After your first sign-in you can add a passkey under **Account → Security**.",
                ],
                "setup", "Setup"),

            new("einladung-annehmen", "Accepting an invitation", Icons.Material.Outlined.MarkEmailRead,
                "Invited people",
                "If you are invited, you receive a link by email or another way, for example via messenger. With it you set up your own account with your own encrypted vault.",
                [
                    "Open the invitation link. The page shows who invited you.",
                    "Choose a **master password** and confirm that nobody can reset it.",
                    "Print or copy down the **recovery kit** and type in the requested words to check.",
                    "Set up the **authenticator app**: scan the QR code, enter the code.",
                    "Done. Your browser has created your keys; the browser of the inviting person checks them automatically the next time they sign in.",
                ],
                [
                    "The link is valid only for a limited time and only once. If it has expired, ask the inviting person for a new one.",
                    "You see nothing of other vaults until someone releases something to you.",
                ]),
        ]),

        new("anmelden", "Signing in and security",
            "Signing in, unlocking, password and second factor.",
        [
            new("anmelden", "Signing in and out", Icons.Material.Outlined.Login,
                "Everyone",
                "Signing in has two steps: master password, then the second factor. The password never leaves your browser; it derives the key to your vault from it.",
                [
                    "Enter your email address and **master password** and choose **Sign in**.",
                    "Enter the 6-digit code from the authenticator app and choose **Confirm**, or **Sign in with passkey**.",
                    "To sign out, click your picture or the account icon at the top right and choose **Sign out**, or use **Account → Sign out** in the sidebar.",
                ],
                [
                    "After five failed attempts the account is locked for 15 minutes.",
                    "After a longer period of inactivity myLife signs you out and removes all keys from memory. The administrator sets the time.",
                    "Every sign-in counts as a life sign for your emergency access.",
                ],
                "login", "Sign in"),

            new("entsperren", "Unlocking the vault", Icons.Material.Outlined.LockOpen,
                "Everyone",
                "If you reload the page you stay signed in, but the keys are gone from memory. The vault is then **locked**.",
                [
                    "The page **Unlock vault** appears by itself.",
                    "Enter your **master password** and choose **Unlock**. No second factor is needed for this.",
                ],
                [
                    "Keys are kept only in the browser's working memory, never in local storage.",
                ]),

            new("passwort-vergessen", "Forgotten password", Icons.Material.Outlined.Restore,
                "Everyone with a recovery kit",
                "With the 24 words from the recovery kit you set a new master password. Your vault stays unchanged.",
                [
                    "On the sign-in page choose **Forgot password?**.",
                    "Enter your email address and the **24 words**, separated by spaces or lines. The first four letters of each word are enough.",
                    "Choose a **new master password** and **Set new password**.",
                    "Sign in with the new password and your second factor.",
                ],
                [
                    "Without the master password and without the kit nobody can get into the vault, not even the administrator.",
                    "The second factor stays. If it is lost too, the administrator can reset it.",
                ],
                "recover", "Recover access"),

            new("passwort-aendern", "Changing the master password", Icons.Material.Outlined.Password,
                "Everyone",
                "Changing it only re-wraps the key of your vault; the data itself does not change.",
                [
                    "Open **Account → Security**.",
                    "Under **Change master password**, enter the current password and the new one twice.",
                    "Choose **Change password**. Other signed-in devices are signed out.",
                ],
                [
                    "The recovery kit stays valid.",
                    "A backup (JSON) still opens with the password that was valid at the time of the export.",
                ],
                "account/security", "Security"),

            new("passkeys", "Setting up passkeys", Icons.Material.Outlined.Fingerprint,
                "Everyone",
                "A passkey replaces the code from the authenticator app when signing in, for example with fingerprint, face or a security key. You still need the master password.",
                [
                    "Open **Account → Security**.",
                    "Under **Passkeys**, enter a name such as “Laptop” and choose **Add passkey**.",
                    "Confirm the dialog of your browser or operating system.",
                    "To remove a passkey, click delete next to it.",
                ],
                [
                    "The authenticator app stays set up as a fallback.",
                ],
                "account/security", "Security"),
        ]),

        new("tresor", "Filling your vault",
            "Entries, documents, wishes and what does not belong in it.",
        [
            new("eintrag-anlegen", "Creating an entry", Icons.Material.Outlined.NoteAdd,
                "Everyone",
                "Every entry belongs to an **area** and has a **type**, such as bank account, insurance or contract. The type determines the fields of the data sheet.",
                [
                    "In the sidebar under **Areas**, open the area.",
                    "Choose the large **New …** button, or another type of the area via the arrow next to it.",
                    "Fill in the fields. Required fields are marked; IBAN, BIC, email addresses and dates are checked.",
                    "Under **For all entries**, add title, notes, where the original is kept, expiry date and follow-up as needed.",
                    "**Save**. The entry is encrypted in your browser.",
                ],
                [
                    "If typical entries are missing in an area, it shows them as buttons. One click creates the matching entry.",
                    "Some fields refer to another entry, for example a loan to its payment protection insurance. In the data sheet one click takes you there.",
                ]),

            new("bearbeiten-beenden", "Editing, ending and deleting", Icons.Material.Outlined.EditNote,
                "Everyone",
                "An entry that no longer exists, such as a cancelled contract, can be **ended** instead of deleted. It is kept for the record but no longer appears in the list.",
                [
                    "Open the entry, choose **Edit**, make your changes, **Save**.",
                    "To end it: **More actions** (three dots) → **End**. **Reactivate** brings it back.",
                    "To delete it: **More actions** → **Delete**. The entry and its documents go to the **trash**.",
                    "In an area's list, **Select** lets you end or delete several entries at once.",
                ],
                [
                    "The list shows ended entries with **Show ended**.",
                    "If someone has changed the entry elsewhere in the meantime, myLife tells you when saving; reload and apply your change again.",
                ]),

            new("papierkorb", "Using the trash", Icons.Material.Outlined.Delete,
                "Everyone",
                "Deleted entries and documents stay in the trash for 30 days and are then removed permanently.",
                [
                    "Open **Administration → Trash**.",
                    "For the entry choose **Restore**, or **Delete permanently** if it should disappear right away.",
                ],
                [],
                "trash", "Trash"),

            new("dokumente", "Adding documents", Icons.Material.Outlined.AttachFile,
                "Everyone",
                "You can attach files to every entry: PDFs, photos, scans. Your browser encrypts them in blocks before they leave your computer.",
                [
                    "Open the entry and go to **Documents**.",
                    "Choose **Add file**, or **Take photo** on your phone.",
                    "Add a label and date and choose **Encrypt and upload**.",
                    "To view, **Preview** (PDF and images); to download, **Save**.",
                ],
                [
                    "A file may be up to 25 MB.",
                    "If your browser shows no preview, save the file instead.",
                    "Search does not look inside PDFs. Important details therefore belong in the data sheet too.",
                ]),

            new("wuensche-verweise", "Wishes, links and extra fields", Icons.Material.Outlined.Lightbulb,
                "Everyone",
                "Besides its fields, every entry has a **wish for your survivors**, such as “cancel”, “take over”, “close” or “memorialise”. The checklist uses these wishes to show the matching entries for a task.",
                [
                    "Edit the entry and choose an option under **Wish for your survivors**.",
                    "Under **Note on your wish**, add what matters, for example who should receive something.",
                    "Under **Linked to**, select other entries that belong to it. The link is visible in both data sheets.",
                    "If a field is missing from the form, choose **Add field** and enter a label and value.",
                ],
                []),

            new("eigenes-datenblatt", "My details", Icons.Material.Outlined.Badge,
                "Everyone",
                "Your surname, first name and a picture are stored encrypted in the “About me” area. Anyone that area is released to sees them too.",
                [
                    "Open **Account → Details**.",
                    "Enter surname and first name and **Save**.",
                    "**Choose picture**: JPEG, PNG or WebP up to 5 MB. A new picture moves the old one to the trash.",
                ],
                [],
                "account/profile", "Details"),

            new("gehoert-nicht-hinein", "What does not belong in it", Icons.Material.Outlined.Block,
                "Everyone",
                "myLife tells your family **what exists and where it is**. Credentials that give someone instant access to money do not belong in it.",
                [
                    "No **PINs**, **TANs** or **full card numbers**; for cards only the last four digits.",
                    "No **individual passwords**. Instead, record in the “Password manager” area which one you use and where its emergency access is kept.",
                    "No **seed phrases**, only where they are kept.",
                    "No **originals** of certificates, only where they are kept.",
                ],
                [
                    "When you save, your browser checks the free texts and warns you, for example about a card number with a valid check digit or a sequence of 12 to 24 words. It only saves if you choose **Save anyway**.",
                ],
                "hints/not-in-vault", "What does not belong in myLife?"),
        ]),

        new("ueberblick", "Keeping track",
            "Overview, completeness, reminders, search and activity log.",
        [
            new("uebersicht", "Overview and completeness", Icons.Material.Outlined.Dashboard,
                "Everyone",
                "The **overview** is where you start. It shows a tile per area with the number of entries, how complete the area is and when you last reviewed it.",
                [
                    "Open **Overview**.",
                    "If a tile says **Review due**, open the area and go through it.",
                    "Missing required and recommended fields are marked in the list; typical missing entries appear as buttons above it.",
                    "When everything is up to date, choose **Mark as reviewed** on the tile. Individual entries are marked in their data sheet.",
                ],
                [
                    "The top of the overview shows what expires soon and which follow-ups are due.",
                ],
                "./", "Overview"),

            new("erinnerungen", "Setting up reminders", Icons.Material.Outlined.NotificationsActive,
                "Everyone",
                "myLife reminds you by email when areas are due for review, before something expires and when a follow-up is due.",
                [
                    "Open **Account → Reminders**.",
                    "Set **Review areas every** … months. 0 switches this reminder off; the default is 6 months.",
                    "Set **Remind before an expiry date** … days, between 7 and 365; the default is 60 days.",
                    "Switch **Reminders by email** on or off and **Save**.",
                ],
                [
                    "Every entry has an expiry date under **For all entries**; for ID documents and cards it is called “Valid until”.",
                    "Of your entries, the server only knows the next date, not what it belongs to. That is why the email contains no details.",
                ],
                "account/reminders", "Reminders"),

            new("wiedervorlage", "Follow-ups", Icons.Material.Outlined.EventRepeat,
                "Everyone",
                "A follow-up reminds you about a single entry, for example “check premium” or “compare contract”.",
                [
                    "Edit the entry and, under **For all entries**, enter **Follow up on** and a note.",
                    "From 14 days before, the follow-up appears on the overview; on the day you get an email.",
                    "Once done, choose **Follow-up done** in the data sheet.",
                ],
                []),

            new("suche", "Searching", Icons.Material.Outlined.Search,
                "Everyone",
                "**Search** finds entries across all fields, notes and document labels.",
                [
                    "Open **Search**.",
                    "Enter a search term, for example a bank, a policy number or a name.",
                    "Click a result to open the entry.",
                ],
                [
                    "Search runs only in your browser over the decrypted entries. It does not search the content of PDFs.",
                ],
                "search", "Search"),

            new("protokoll", "Viewing the activity log", Icons.Material.Outlined.History,
                "Everyone",
                "The **activity log** shows who did what in your vault and when: sign-ins, changes, exports, releases and every access by trusted persons.",
                [
                    "Open **Administration → Activity log**.",
                    "Entries are sorted by time; your browser adds the titles.",
                ],
                [
                    "The log contains no content and only shortened IP addresses.",
                ],
                "account/audit", "Activity log"),
        ]),

        new("familie", "Family and trusted persons",
            "Inviting, checking keys, releases, letters and the checklist.",
        [
            new("einladen", "Inviting someone", Icons.Material.Outlined.PersonAdd,
                "Everyone",
                "Every person gets their own account with their own vault. Later you can appoint them as a trusted person. There is no public registration.",
                [
                    "Open **Family** and choose **Invite someone**.",
                    "Enter the email address and the **language of the invitation**.",
                    "**Send by email from myLife** or **Share the link yourself**. For the link: **Copy link** and send it your own way.",
                    "**Invite**. **Your invitations** shows the status.",
                ],
                [
                    "Sending the link your own way is safest: the secret in the link is created only in your browser and the server never sees it.",
                    "If a link has expired, choose **Send a new link by email** or **New link to share** for the invitation. **Withdraw invitation** invalidates the link.",
                    "Without email set up, only the link to share works.",
                ],
                "family", "Family"),

            new("schluessel", "Checking keys", Icons.Material.Outlined.VerifiedUser,
                "Everyone",
                "So that nobody can slip in a false key, myLife checks everyone's keys automatically. myLife only releases anything to confirmed keys.",
                [
                    "Open **Family**. The **Key** column shows the status for each person.",
                    "**Confirmed by you**: your browser checked the key itself during the invitation and remembered it.",
                    "**Confirmed by …**: someone you trust has countersigned it.",
                    "**Not yet confirmed**: this is not an error. When you appoint the person as trusted, your browser remembers the key; a later swap is then noticed.",
                ],
                [
                    "**Key changed!** means the server shows a different key from the one your browser remembered. myLife creates no releases for it. Talk to the person through another channel and contact the administrator.",
                    "Your own fingerprint is shown at the top of the page.",
                ],
                "family", "Family"),

            new("vertrauenspersonen", "Appointing trusted persons and releasing areas", Icons.Material.Outlined.VolunteerActivism,
                "Everyone",
                "In an emergency a trusted person sees exactly the areas you released to them for that case. **Deceased** and **incapacitated** are separate cases.",
                [
                    "Open **Trusted persons** and choose **Add trusted person**, or click an existing one.",
                    "Enter the **relationship** and, if you like, a **note**.",
                    "Switch on **May request access** if the person should be able to request access themselves in an emergency.",
                    "Under **Areas**, tick the cases per area: **Deceased**, **Incapacitated**, **Share now**. **all** ticks a whole column.",
                    "**Save and encrypt**.",
                ],
                [
                    "To remove someone, choose **Remove as trusted person**. All releases for this person are deleted.",
                    "If it says **Needs saving again**, the person has a new key. Open them and save once more.",
                    "Emergency access becomes active as soon as you appoint the first trusted person.",
                ],
                "trust", "Trusted persons"),

            new("briefe", "Personal letters", Icons.Material.Outlined.Mail,
                "Everyone",
                "A letter is an entry in the “Personal wishes” area. Each letter has its own key and goes only to the people you choose for it.",
                [
                    "Under **Personal wishes**, create a **Letter** with recipient and text.",
                    "Open **Trusted persons** and click the person.",
                    "Under **Personal letters**, tick the letter for the matching case and **Save and encrypt**.",
                ],
                [
                    "The recipient field in the letter is only a note. Who may read it is decided solely under trusted persons.",
                    "In reading mode, letters appear under **Letters to you**.",
                ]),

            new("sofort-teilen", "Share now", Icons.Material.Outlined.FolderShared,
                "Everyone",
                "Some things your family should know today, such as important contacts. With **Share now** the person can read an area straight away but cannot change anything.",
                [
                    "Open **Trusted persons** and click the person.",
                    "Tick **Share now** for the area and **Save and encrypt**.",
                    "The person sees the area under **Shared with me**.",
                ],
                [
                    "If you stop sharing, your browser creates a new key for the area and re-encrypts it. This may take a moment.",
                ],
                "trust", "Trusted persons"),

            new("checkliste", "Maintaining the checklist", Icons.Material.Outlined.Checklist,
                "Everyone",
                "The checklist tells your trusted persons what to do and when. Each task is an entry with a time frame, a responsible person and linked entries.",
                [
                    "Under **Areas**, open the **Checklist**.",
                    "The first time, choose **Use template** (template for Germany) or **Start without template**.",
                    "Open and edit a task: **When** (first 24 hours, first week, first month, later), text, **Responsible**.",
                    "Under **Related entries from area**, choose an area and, if needed, **Only entries with the wish**, for example “cancel”.",
                    "Add your own tasks with **New task**.",
                ],
                [
                    "Release the checklist under **Trusted persons** like any other area. People only see linked entries from areas they are allowed to see.",
                    "The template is not legal advice; the deadlines are guidelines.",
                ],
                "checklist", "Checklist"),
        ]),

        new("notfall", "Emergency access",
            "Life sign, deadlines, requests, objection and revoking.",
        [
            new("lebenszeichen", "Giving a life sign", Icons.Material.Outlined.Favorite,
                "Everyone with trusted persons",
                "As long as you check in regularly, nothing happens. **Every sign-in counts as a life sign.**",
                [
                    "Simply sign in, or",
                    "click the link in the reminder email, or",
                    "choose **Confirm I am well** under **Emergency access**.",
                ],
                [
                    "**Your emergency access** shows the last life sign, when the next one is due and the earliest release date without a response.",
                    "The link in the email only records the life sign and works only once.",
                ],
                "emergency", "Emergency access"),

            new("fristen", "Choosing your deadlines", Icons.Material.Outlined.Timer,
                "Everyone with trusted persons",
                "You decide how long myLife waits before releasing anything, within the limits set by the administrator.",
                [
                    "Open **Emergency access** and go to **Your deadlines**.",
                    "**Life sign**: how often you must check in (default: 30 days).",
                    "**Waiting period**: how long to wait after the last unanswered reminder (default: 14 days).",
                    "**Waiting period after a request**: how long you can object to a request (default: 7 days).",
                    "Optionally enter a **second email address** and an **ntfy topic** for push messages to your phone, then **Save**.",
                ],
                [
                    "Reminders come in stages after the due date, usually after 3, 7 and 14 days; the administrator sets the stages.",
                    "If sending email fails, no deadlines run on. A mail outage never leads to a release.",
                ],
                "emergency", "Emergency access"),

            new("antrag-widersprechen", "Objecting to a request", Icons.Material.Outlined.FrontHand,
                "Everyone for whom someone has requested access",
                "When a trusted person requests access, you immediately get an email with an **object** link. All other trusted persons are informed as well.",
                [
                    "Click the object link in the email. The request is now **paused**; nothing is released for the time being.",
                    "Sign in and open **Emergency access**.",
                    "Choose **Object for good** if you are well, or **Let it continue** if access is fine.",
                ],
                [
                    "While a request is running, the **overview** shows a clear notice at the top.",
                    "Without an objection, only the requested case is released after the request period.",
                ],
                "emergency", "Emergency access"),

            new("freigabe-zuruecknehmen", "Revoking a release", Icons.Material.Outlined.Undo,
                "Everyone whose emergency access has been released",
                "If your emergency access was released although you are well, for example after an illness, you revoke the release.",
                [
                    "Sign in and open **Emergency access**.",
                    "Choose **Revoke release** and confirm.",
                    "Keep the page open until the message says all areas and letters have new keys.",
                ],
                [
                    "Your trusted persons can then no longer read anything intended only for an emergency. What you share now stays shared.",
                    "If the key change is interrupted, continue it under **Emergency access** with **Continue key change**.",
                    "A planned joint deletion is cancelled by revoking.",
                ],
                "emergency", "Emergency access"),
        ]),

        new("vertrauensperson", "As a trusted person",
            "Reading what is shared, requesting access, using reading mode.",
        [
            new("mit-mir-geteilt", "Shared with me", Icons.Material.Outlined.FolderShared,
                "Trusted persons",
                "What others already share with you today is under **Shared with me**, read-only. It is decrypted in your browser with your own key.",
                [
                    "Open **Shared with me**.",
                    "Expand the person and the area, then open an entry.",
                ],
                [
                    "If it says the key is not confirmed or has changed, check on the **Family** page that everything is in order.",
                ],
                "shared", "Shared with me"),

            new("zugang-beantragen", "Requesting access", Icons.Material.Outlined.HealthAndSafety,
                "Trusted persons with the right to request",
                "Only request access in an emergency. The person and their other trusted persons are informed immediately and can object.",
                [
                    "Open **Emergency access** and go to **For others**.",
                    "For the person, choose **Request access** and state the case: **Deceased** or **Incapacitated**.",
                    "Confirm with **Request**.",
                    "Without an objection, access is released after the request period. You get an email and find the person in **Reading mode**.",
                ],
                [
                    "As long as the request is running you can **withdraw** it.",
                    "If the button is missing, the person has not given you the right to request. Access then only comes if their life sign stops.",
                ],
                "emergency", "Emergency access"),

            new("lesemodus", "Reading in reading mode", Icons.Material.Outlined.MenuBook,
                "Trusted persons after a release",
                "Reading mode is a calm interface for an emergency. It guides you along the checklist through everything released to you.",
                [
                    "Open **Reading mode** and choose **Open** for the person.",
                    "Read the introduction and go step by step with **Next**; **Back** goes one step back.",
                    "For checklist tasks, look at the matching entries and tick **Done**. All trusted persons see the same state.",
                    "View or save documents.",
                    "**Larger text** and **Smaller text** change the text size; **Leave reading mode** takes you back.",
                ],
                [
                    "You can stop at any time and continue later.",
                    "In the case “incapacitated” the person can withdraw access again later.",
                ],
                "reading", "Reading mode"),

            new("gemeinsam-loeschen", "Deleting a vault together", Icons.Material.Outlined.DeleteForever,
                "Trusted persons after the release “deceased”",
                "When everything is done, you can delete the vault together for good. All trusted persons for the case “deceased” must agree.",
                [
                    "First save what you want to keep, for example documents in reading mode.",
                    "Open **Emergency access** and, for the person, under **Delete vault for good**, choose **Agree to deletion**.",
                    "Once everyone has agreed, everything is deleted seven days later: data, documents, letters.",
                ],
                [
                    "Until then anyone can cancel with **Withdraw agreement**.",
                    "The vault of the only administrator cannot be deleted this way; someone else must become administrator first.",
                ],
                "emergency", "Emergency access"),
        ]),

        new("export", "Export and paper",
            "Emergency folder, data sheet, backup, emergency package and mylife-decrypt.",
        [
            new("notfallordner", "Creating the emergency folder", Icons.Material.Outlined.PictureAsPdf,
                "Everyone",
                "The emergency folder combines all or selected areas: table of contents, checklist, data sheets and a list of documents. It is created entirely in your browser.",
                [
                    "Open **Export & print**.",
                    "Under **Emergency folder**, select the **areas** and, if needed, **Include ended entries**.",
                    "**Create PDF**, set a password for the PDF (at least 8 characters, no accented letters, not your master password) and save it.",
                    "Or choose **Print view** for the paper folder and print from there.",
                ],
                [
                    "The PDF is encrypted with AES-256. myLife does not store the password.",
                    "A PDF without a password and every printout contain your data in plain text.",
                ],
                "export", "Export & print"),

            new("datenblatt", "Printing a data sheet", Icons.Material.Outlined.Print,
                "Everyone",
                "You can print any single entry as a data sheet or save it as a PDF, for example for the bank.",
                [
                    "Open the entry.",
                    "**More actions** → **Print** or **Data sheet as PDF**.",
                ],
                []),

            new("datensicherung", "Exporting and restoring a backup", Icons.Material.Outlined.DataObject,
                "Everyone",
                "The backup is an open, encrypted JSON format with all entries and documents. Your data is never locked into myLife.",
                [
                    "Open **Export & print** and go to **Backup (JSON)**.",
                    "If needed, **Include documents**, then **Download export**.",
                    "To restore, under **Restore backup** choose **Choose file**, enter the master password from that time or the 24 words, and choose **Import**.",
                ],
                [
                    "The file opens with the master password valid at the time of the export or with your recovery kit, even without the server.",
                    "When restoring, anything already present is skipped.",
                ],
                "export", "Export & print"),

            new("notfallpaket", "Creating an emergency package", Icons.Material.Outlined.Inventory,
                "Everyone",
                "The emergency package is a single HTML file for your family that works without the myLife server and without the internet. It only opens with a printed key sheet.",
                [
                    "Open **Export & print** and choose **Create emergency package**.",
                    "Select the areas and, if needed, **Include documents**.",
                    "**Create and save package**.",
                    "**Print key sheet** right away. You see the 24 words only here; myLife does not store them anywhere.",
                    "Give the file to your family, for example on a USB stick, and keep the sheet separately, for example in a safe deposit box or with a notary.",
                ],
                [
                    "To open it, open the file in a browser (double-click), enter the 24 words and choose **Open**.",
                    "The package is a snapshot. After important changes create a new one and replace file and sheet together; every package has its own key.",
                ],
                "export/package", "Emergency package"),

            new("mylife-decrypt", "Opening exports without the server", Icons.Material.Outlined.Terminal,
                "Relatives, successors",
                "The **mylife-decrypt** tool opens backups and emergency packages even when the server no longer exists. It needs neither the internet nor .NET.",
                [
                    "Download the file for Windows, macOS or Linux from the myLife GitHub release; ideally a copy is already kept with the emergency folder.",
                    "Run the tool with the export file and enter the master password, the 24 words of the recovery kit or the 24 words of the package key.",
                    "It writes the entries and documents to a folder in readable form.",
                ],
                [
                    "The exact usage is described in the operations documentation under `docs/betrieb/mylife-decrypt.md`.",
                ]),
        ]),

        new(AdministrationPart, "Administration",
            "For administrators: installation, email, deadlines, backup, users and updates.",
        [
            new("installation", "Installing myLife", Icons.Material.Outlined.Dns,
                "Whoever runs the instance",
                "myLife is self-hosted with Docker Compose: the app and a PostgreSQL database. In front of it a reverse proxy provides HTTPS, for example Nginx Proxy Manager on a Synology.",
                [
                    "Create a project folder and put `docker-compose.yml` and `.env.example` from the repository into it; rename `.env.example` to `.env`.",
                    "In `.env`, set at least `POSTGRES_PASSWORD`, `MYLIFE_PORT` and `MYLIFE_BACKUP_KEY`. Keep the backup key separate from the NAS.",
                    "Give the folder `data/app` to the user with UID 1654.",
                    "Start the project, for example in Container Manager. `/health/ready` then returns `Healthy`.",
                    "In the reverse proxy, route the domain with a Let's Encrypt certificate to the app's port and allow uploads of up to 30 MB.",
                    "Open the address and run the **setup wizard**.",
                ],
                [
                    "The detailed instructions are in `docs/betrieb/installation.md`, the handover to a successor in `docs/betrieb/nachfolge.md`.",
                ]),

            new("mailversand", "Setting up email", Icons.Material.Outlined.Email,
                "Administrators",
                "myLife sends emails for invitations, life signs, reminders and emergency access. **Without email, emergency access deadlines do not run on.**",
                [
                    "Open **Administration → Settings**.",
                    "Tab **General**: enter the **address of this instance**, for example `https://mylife.example.org`.",
                    "Tab **Email**: SMTP server, port, encryption (STARTTLS usually on 587, SSL/TLS usually on 465), username, password, sender address and sender name.",
                    "**Save and send test email**. The test email goes to your own address; any error appears immediately.",
                    "Optionally, tab **ntfy**: server and access token for push messages, then **Save and send test**.",
                ],
                [
                    "**Queue** shows waiting and abandoned emails; **Retry now** gives them a push.",
                    "Stored passwords appear as “stored”. Leaving the field empty keeps the stored value.",
                ],
                "admin/settings", "Settings"),

            new("fristen-grenzen", "Setting deadlines and sign-out time", Icons.Material.Outlined.Tune,
                "Administrators",
                "You set defaults and limits for emergency access. Every user chooses their own values within these limits; you cannot force shorter deadlines.",
                [
                    "Open **Administration → Settings → Deadlines**.",
                    "For **Life sign**, **Waiting period** and **Waiting period after a request**, set **Minimum**, **Default** and **Maximum**.",
                    "Under **Reminders (days after expiry)**, enter the stages in ascending order, separated by commas, for example `3, 7, 14`.",
                    "On the **General** tab, set the **automatic sign-out** in minutes and the **log retention**.",
                    "**Save**.",
                ],
                []),

            new("sicherung", "Checking and restoring backups", Icons.Material.Outlined.Backup,
                "Administrators, whoever runs the instance",
                "If `MYLIFE_BACKUP_KEY` is set, myLife stores an encrypted backup in the `backup` folder every day: database, key ring and, if you wish, the documents.",
                [
                    "Open **Administration → Settings → Backup**.",
                    "Check the last backup, the next run and any error. **Back up now** creates one immediately.",
                    "Set the time, the number of daily, weekly and monthly backups to keep, and **Include documents in every backup**.",
                    "To restore, stop the project and run `docker compose run --rm app restore /data/backup/<file>.mlbak --confirm` via SSH.",
                ],
                [
                    "Without the backup key no backup can be restored. Keep it separate from the NAS.",
                    "The backups also belong in a second place, for example with Hyper Backup.",
                    "If a backup fails, the administrators get an email.",
                ],
                "admin/settings", "Settings"),

            new("benutzer", "Managing users", Icons.Material.Outlined.People,
                "Administrators",
                "**Users** shows all accounts with names, addresses and sign-in details, but no vault contents. Every action is logged and the person concerned gets an email.",
                [
                    "Open **Administration → Users**.",
                    "Under **Actions** for each account: **Lock** and **Unlock**, **Reset two-factor sign-in**, **Make administrator** or **Remove administrator rights**, **Request deletion**.",
                    "Under **Open invitations**, withdraw invitations.",
                ],
                [
                    "Only reset two-factor sign-in if you are sure it really is the person.",
                    "Deletion only happens once the person confirms the link in the email themselves.",
                    "There must always be at least one active administrator. Two are better.",
                    "The **System log** under **Administration** shows administrative actions and system events.",
                ],
                "admin/users", "Users"),

            new("update", "Installing updates", Icons.Material.Outlined.SystemUpdateAlt,
                "Whoever runs the instance",
                "Updates come as a new image. Database migrations run automatically on start-up.",
                [
                    "Under **Administration → Settings → Backup**, choose **Back up now**.",
                    "Stop the project, update the image and start the project again, or via SSH run `docker compose pull` and `docker compose up -d`.",
                    "Sign in briefly and check under **Administration** that email and backup show no errors.",
                ],
                [
                    "With a fixed version in `MYLIFE_VERSION`, an update only comes when you change the number.",
                    "Migrations only run forwards. To go back, use the old version and the backup from before the update.",
                ]),
        ]),
    ];
}
