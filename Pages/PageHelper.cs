using HomeSeerAPI;
using NullGuard;
using Scheduler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using static System.FormattableString;

namespace Hspi.Pages
{
    internal class PageHelper : PageBuilderAndMenu.clsPageBuilder
    {
        public PageHelper(IHSApplication HS, PluginConfig pluginConfig, string pageName) : base(pageName)
        {
            this.HS = HS;
            this.pluginConfig = pluginConfig;
        }

        public static string HtmlEncode<T>([AllowNull]T value)
        {
            if (value == null)
            {
                return string.Empty;
            }
            return HttpUtility.HtmlEncode(value);
        }

        protected static string HtmlTextBox(string name, string defaultText, int size = 25, string type = "text", bool @readonly = false)
        {
            return Invariant($@"<input type='{type}' id='{NameToIdWithPrefix(name)}' size='{size}' name='{name}' value='{HtmlEncode(defaultText)}' {(@readonly ? "readonly" : string.Empty)}>");
        }

        protected static string NameToId(string name)
        {
            return name.Replace(' ', '_');
        }

        protected static string NameToIdWithPrefix(string name)
        {
            return Invariant($"{IdPrefix}{NameToId(name)}");
        }

        protected string FormCheckBox(string name, string label, bool @checked, bool autoPostBack = false)
        {
            var cb = new clsJQuery.jqCheckBox(name, label, PageName, true, true)
            {
                id = NameToIdWithPrefix(name),
                @checked = @checked,
                autoPostBack = autoPostBack,
            };
            return cb.Build();
        }

        protected string FormPageButton(string name, string label)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, true)
            {
                id = NameToIdWithPrefix(name),
            };

            return b.Build();
        }

        protected string PageTypeButton(string name, string label, string pageType, string deviceId = null)
        {
            var b = new clsJQuery.jqButton(name, label, PageName, false)
            {
                id = NameToIdWithPrefix(name),
                url = Invariant($@"/{HttpUtility.UrlEncode(ConfigPage.Name)}?{PageTypeId}={HttpUtility.UrlEncode(pageType)}&{DeviceIdId}={HttpUtility.UrlEncode(deviceId ?? string.Empty)}"),
            };

            return b.Build();
        }

        protected const string DeviceIdId = "DeviceIdId";
        protected const string PageTypeId = "type";
        protected readonly IHSApplication HS;
        protected readonly PluginConfig pluginConfig;
        private const string IdPrefix = "id_";
    }
}