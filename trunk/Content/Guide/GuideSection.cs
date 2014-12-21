﻿using System.Collections.Generic;
using System.Xml.Linq;

namespace EPubLibrary.Content.Guide
{
    public class GuideSection
    {
        private readonly List<GuideElement> _content = new List<GuideElement>();

        public XElement GenerateGuide()
        {
            var xGuide = new XElement(EPubNamespaces.OpfNameSpace + "guide");
            foreach (var guideElement in _content)
            {
                if (guideElement.IsValid())
                {
                    var xRef = new XElement(EPubNamespaces.OpfNameSpace + "reference");
                    xRef.Add(new XAttribute("type",guideElement.GetTypeAsString()));
                    xRef.Add(new XAttribute("title",guideElement.Title));
                    xRef.Add(new XAttribute("href",guideElement.Link));
                    xGuide.Add(xRef);
                }
            }
            return xGuide;
        }

        public void AddGuideItem(string link, string title, GuideTypeEnum type)
        {
            var element = new GuideElement { Link = link.Replace('\\', '/'), Title = title, Type = type };
            _content.Add(element);
        }

        public bool HasData()
        {
            return (_content.Count>0);
        }
    }
}
