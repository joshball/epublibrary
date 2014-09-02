﻿using System.Collections.Generic;
using System.Text;
using EPubLibrary.PathUtils;
using XHTMLClassLibrary.BaseElements;
using XHTMLClassLibrary.BaseElements.BlockElements;
using XHTMLClassLibrary.BaseElements.InlineElements;
using EPubLibrary.Content.Guide;
using XHTMLClassLibrary.BaseElements.InlineElements.TextBasedElements;

namespace EPubLibrary.XHTML_Items
{
    public class TitlePageFile : BaseXHTMLFile
    {
        private readonly List<string> _authors = new List<string>();
        private readonly List<string> _series = new List<string>();

        public TitlePageFile(HTMLElementType compatibility)
            : base(compatibility)
        {
            pageTitle = "Title";
            DocumentType = GuideTypeEnum.TitlePage;
            FileName = "title.xhtml";
            FileEPubInternalPath = new EPubInternalPath(EPubInternalPath.DefaultOebpsFolder + "/text/");
            Id = "title";
        }

        public string BookTitle { get; set; }

        public List<string> Authors { get { return _authors; } }

        public List<string> Series { get { return _series; } }

        public override void GenerateBody()
        {
            base.GenerateBody();
            Div titlePage = new Div();
            titlePage.GlobalAttributes.Class.Value = "titlepage";
            if (!string.IsNullOrEmpty(BookTitle))
            {
                // try to use FB2 book's title
                var p = new H2();
                p.Add(new SimpleHTML5Text { Text = BookTitle });
                string itemClass = string.Format("title{0}", 1);
                p.GlobalAttributes.Class.Value = itemClass;
                titlePage.Add(p);
            }
            else
            {
                titlePage.Add(new SimpleHTML5Text { Text = "Unnamed" });
            }

            titlePage.Add(new EmptyLine());

            StringBuilder sbSeries = new StringBuilder();
            foreach (var serie in _series)
            {
                if (!string.IsNullOrEmpty(sbSeries.ToString()))
                {
                    sbSeries.Append(" , ");
                }
                sbSeries.Append(serie);
            }
            if (sbSeries.ToString() != string.Empty)
            {
                var seriesItem = new SimpleHTML5Text { Text = string.Format("( {0} )", sbSeries) };
                var containingText = new EmphasisedText();
                containingText.Add(seriesItem);
                H3 seriesHeading = new H3();
                seriesHeading.GlobalAttributes.Class.Value = "title_series";
                seriesHeading.Add(containingText);
                titlePage.Add(seriesHeading);
            }

            foreach (var author in _authors)
            {
                var authorsHeading = new H3();
                var authorLine = new SimpleHTML5Text { Text = author };
                authorsHeading.Add(authorLine);
                authorsHeading.GlobalAttributes.Class.Value = "title_authors";
                titlePage.Add(authorsHeading);
            }


            BodyElement.Add(titlePage);
        }

    }
}
