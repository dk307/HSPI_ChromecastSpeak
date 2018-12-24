using HomeSeerAPI;
using Hspi.Utils;
using NullGuard;
using System;
using System.Collections.Specialized;
using System.Text;

namespace Hspi.Pages
{
    internal class ActionPage : PageHelper
    {
        public ActionPage(IHSApplication HS, PluginConfig pluginConfig) : base(HS, pluginConfig, "Events")
        {
        }

        public IPlugInAPI.strMultiReturn GetRefreshActionPostUI([AllowNull] NameValueCollection postData, IPlugInAPI.strTrigActInfo actionInfo)
        {
            IPlugInAPI.strMultiReturn result = default;
            result.DataOut = actionInfo.DataIn;
            result.TrigActInfo = actionInfo;
            result.sResult = string.Empty;
            if (postData != null && postData.Count > 0)
            {
                var action = (actionInfo.DataIn != null) ?
                                                    (ChromecastCastAction)ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) :
                                                    new ChromecastCastAction();

                foreach (var pair in postData)
                {
                    string text = Convert.ToString(pair);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (text.StartsWith(nameof(ChromecastCastAction.ChromecastDeviceId)))
                        {
                            action.ChromecastDeviceId = postData[text];
                        }
                        else if (text.StartsWith(nameof(ChromecastCastAction.Url)))
                        {
                            action.Url = postData[text];
                        }
                        else if (text.StartsWith(nameof(ChromecastCastAction.ContentType)))
                        {
                            action.ContentType = postData[text];
                        }
                        else if (text.StartsWith(nameof(ChromecastCastAction.Live)))
                        {
                            action.Live = postData[text] == "checked";
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(action.Url))
                {
                    result.sResult += "<BR>Url is not valid";
                }

                if (string.IsNullOrWhiteSpace(action.ChromecastDeviceId))
                {
                    result.sResult += "<BR>Chromecast device is not valid";
                }
                result.DataOut = ObjectSerialize.SerializeToBytes(action);
            }

            return result;
        }

        public string GetRefreshActionUI(string uniqueControlId, IPlugInAPI.strTrigActInfo actionInfo)
        {
            StringBuilder stb = new StringBuilder();
            var action = ObjectSerialize.DeSerializeFromBytes(actionInfo.DataIn) as ChromecastCastAction;

            var chromecastDevices = new NameValueCollection();
            foreach (var device in pluginConfig.Devices)
            {
                chromecastDevices.Add(device.Key, device.Value.Name);
            }

            stb.Append(FormDropDown(nameof(ChromecastCastAction.ChromecastDeviceId) + uniqueControlId, chromecastDevices, action?.ChromecastDeviceId, 250, string.Empty, false));
            stb.Append("<p> Url:");
            stb.Append(HtmlTextBox(nameof(ChromecastCastAction.Url) + uniqueControlId, action?.Url, 100));
            stb.Append("</p><p> Content mime type:");
            stb.Append(HtmlTextBox(nameof(ChromecastCastAction.ContentType) + uniqueControlId, action?.ContentType, 50));
            stb.Append("</p><p> Live:");
            stb.Append(FormCheckBox(nameof(ChromecastCastAction.Live) + uniqueControlId, string.Empty, action?.Live ?? false));
            stb.Append("</p>");
            stb.Append(FormPageButton(SaveButtonName + uniqueControlId, "Save"));
            return stb.ToString();
        }

        private NameValueCollection CreateNameValueCreation<T>() where T : Enum
        {
            var collection = new NameValueCollection();

            foreach (var value in EnumUtil.GetValues<T>())
            {
                collection.Add(value.ToString(), value.ToString());
            }

            return collection;
        }

        private const string SaveButtonName = "SaveButton";
    }
}