﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using XHTMLClassLibrary;
using XHTMLClassLibrary.BaseElements;
using XHTMLClassLibrary.BaseElements.BlockElements;
using XHTMLClassLibrary.BaseElements.InlineElements;
using EPubLibrary.Content.Guide;

namespace EPubLibrary.XHTML_Items
{
    public class TitlePageFile : BaseXHTMLFile
    {
        private readonly List<string> authors = new List<string>();
        private readonly List<string> series = new List<string>();

        public TitlePageFile()
        {
            pageTitle = "Title";
            DocumentType = GuideTypeEnum.TitlePage;
            FileName = "title.xhtml";
        }

        public string BookTitle { get; set; }

        public List<string> Authors { get { return authors; } }

        public List<string> Series { get { return series; } }


        override public XDocument Generate()
        {

            Div titlePage = new Div();
            titlePage.Class.Value = "titlepage";
            if (!string.IsNullOrEmpty(BookTitle))
            {
                // try to use FB2 book's title
                IBlockElement p = new H2();
                p.Add(new SimpleEPubText { Text = BookTitle });
                string itemClass = string.Format("title{0}", 1);
                p.Class.Value = itemClass;
                titlePage.Add(p);

                //foreach (var item in BookTitle.SubElements())
                //{
                //    titlePage.Add(item);
                //}
            }
            else
            {
                titlePage.Add(new SimpleEPubText() { Text = "Unnamed" });
            }
            
            titlePage.Add(new EmptyLine());

            StringBuilder sbSeries = new StringBuilder();
            foreach (var serie in series)
            {
                if (!string.IsNullOrEmpty(sbSeries.ToString()))
                {
                    sbSeries.Append(" , ");
                }
                sbSeries.Append(serie);
            }
            if (sbSeries.ToString() != string.Empty)
            {
                SimpleEPubText seriesItem = new SimpleEPubText { Text = string.Format("( {0} )", sbSeries) };
                EmphasisedText containingText = new EmphasisedText();
                containingText.Add(seriesItem);
                H3 seriesHeading = new H3();
                seriesHeading.Add(containingText);
                titlePage.Add(seriesHeading);                
            }

            foreach (var author in authors)
            {
                H3 authorsHeading = new H3();
                SimpleEPubText authorLine = new SimpleEPubText() { Text = author};
                authorsHeading.Add(authorLine);
                titlePage.Add(authorsHeading);
            }


            bodyElement.Add(titlePage);
            
            //return document;
            return base.Generate();
        }


    }
}
