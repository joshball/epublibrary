﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using EPubLibrary.Container;
using EPubLibrary.Content;
using EPubLibrary.CSS_Items;
using EPubLibrary.Template;
using EPubLibrary.TOC;
using EPubLibrary.XHTML_Items;
using FontsSettings;
using ICSharpCode.SharpZipLib.Zip;
using TranslitRu;
using XHTMLClassLibrary.BaseElements;
using XHTMLClassLibrary.BaseElements.InlineElements;
using EPubLibrary.AppleEPubV2Extensions;

namespace EPubLibrary
{
    static public class Logger
    {
        // Create a logger for use in this class
        public static readonly log4net.ILog log = log4net.LogManager.GetLogger(Assembly.GetExecutingAssembly().GetType());       
    }

    internal static class XNodetemExtender
    {
        public static long EstimateSize(this XNode node)
        {
            MemoryStream stream = new MemoryStream();
            using (var writer = XmlWriter.Create(stream))
            {
                node.WriteTo(writer);
            }
            return stream.Length;
        }
    }
    public class EPubFile
    {
        private readonly ZipEntryFactory _zipFactory = new ZipEntryFactory();
        private readonly EPubTitleSettings _title = new EPubTitleSettings();
        private string _coverImage = null;
        private readonly CSSFile _mainCss = new CSSFile() { FileName = "main.css", ID = "mainCSS", FileExtPath = @"main.css",LocationSubFolder = "css"};
        private readonly AdobeTemplate _adobeTemplate = new AdobeTemplate();
        private readonly List<CSSFile> _cssFiles = new List<CSSFile>();
        private readonly List<BookDocument> _sections = new List<BookDocument>();
        private readonly TOCFile _tableOfContentFile = new TOCFile();
        private readonly ContentFile _content = new ContentFile();
        private readonly Rus2Lat _rule = new Rus2Lat();
        private readonly List<string> _allSequences = new List<string>();
        private readonly List<string> _aboutTexts = new List<string>();
        private readonly List<string> _aboutLinks = new List<string>();
        private readonly CSSFontSettingsCollection _fontSettings = new CSSFontSettingsCollection();
        private readonly AppleDisplayOptionsFile _appleOptionsFile = new AppleDisplayOptionsFile();
        private readonly Dictionary<string,EPUBImage> _images = new Dictionary<string ,EPUBImage>();
        private bool _flatStructure = false;

        public List<BookDocument> BookDocuments { get { return _sections; } }


        /// <summary>
        /// Controls if Lord Kiron's license need to be added to file
        /// </summary>
        public bool InjectLKRLicense { get; set; }

        /// <summary>
        /// Return transliteration rule object
        /// </summary>
        public Rus2Lat Transliterator { get { return _rule; } }

        /// <summary>
        /// Get/Set if adobe template XPGT file should be added to resulting file
        /// </summary>
        public bool UseAdobeTemplate { get; set; }

        /// <summary>
        /// Get/Set Path to Adobe template XPGT file
        /// </summary>
        public string AdobeTemplatePath{get;set;}

        /// <summary>
        /// Get/Set "flat" mode , when flat mode is set no subfolders created inside the ZIP
        /// used to work around bugs in some readers
        /// </summary>
        public bool FlatStructure
        {
            get { return _flatStructure; }
            set
            {
                if (value)
                {
                    _mainCss.LocationSubFolder= string.Empty;
                }
                else
                {
                    _mainCss.LocationSubFolder = "css";
                }
                _flatStructure = value;
            }
        }


        /// <summary>
        /// Transliteration mode
        /// </summary>
        public TranslitModeEnum TranslitMode = TranslitModeEnum.ExternalRuleFile;

        // All sequences in the book
        public List<string> AllSequences { get { return _allSequences; } }

        //public List<Font> FontObjects { get { return fontObjects; } }


        public List<CSSFile> CSSFiles { get { return _cssFiles; } }


        /// <summary>
        /// Get access to main CSS file included in all 
        /// xhtml book files 
        /// </summary>
        public CSSFile MainCSS { get { return _mainCss; } }

        /// <summary>
        /// Get access to book's title data
        /// </summary>
        public EPubTitleSettings Title
        {
            get { return _title; }
        }

        /// <summary>
        /// Get/Set embeding styles into xHTML files instead of referencing style files
        /// </summary>
        public bool EmbedStyles { get; set; }

        public Dictionary<string,EPUBImage> Images
        {
            get { return _images; }
        }

        public TitlePageFile TitlePage { get; set; }

        public AnnotationPageFile AnnotationPage { get; set; }

        public bool TranliterateToc { set; get; }

        /// <summary>
        /// Strings added to about page
        /// </summary>
        public List<string> AboutTexts
        {
            get
            {
                return _aboutTexts;
            }
        }

        /// <summary>
        /// Links added to about page
        /// </summary>
        public List<string> AboutLinks
        {
            get
            {
                return _aboutLinks;
            }
        }

        /// <summary>
        /// Returns reference to apple options file
        /// </summary>
        public AppleDisplayOptionsFile AppleOptions
        {
            get { return _appleOptionsFile; }
        }

        public bool IsValid()
        {
            if (!_title.IsValid())
            {
                return false;
            }
            return true;
        }


        /// <summary>
        /// Writes (generates) file to disk
        /// </summary>
        /// <param name="outFileName"></param>
        public void Generate(string outFileName)
        {
            Logger.log.DebugFormat("Generating file : {0}", outFileName);
            if (!IsValid())
            {
                Logger.log.Error("File data not valid. Can't generate. Aborting.");
                return;
            }
            try
            {
                string folder = Path.GetDirectoryName(outFileName);
                if (!string.IsNullOrEmpty(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch (Exception ex)
            {
                Logger.log.ErrorFormat("Error creating folder {0}, exception thrown : {0}", Path.GetDirectoryName(outFileName), ex.ToString());
            }
            try
            {
                using (var fileStream = File.Create(outFileName))
                {
                    using (var s = new ZipOutputStream(fileStream))
                    {
                        // The EPub does not like 64 bit "patched" headers due to mimetype entry
                        s.UseZip64 = UseZip64.Off;
                        AddMimeTypeEntry(s);
                        AddBookData(s);
                        AddMetaData(s);
                        s.Finish();
                    }
                    fileStream.Close();
                }
            }
            catch (Exception ex)
            {
                Logger.log.ErrorFormat("Error generating file, exception thrown : {0}", ex.ToString());
                File.Delete(outFileName);
            }
        }

        private void AddMetaData(ZipOutputStream stream)
        {
            AddMetaDataFolder(stream);
            AddMetaDataFile(stream);
            AddAppleOptionsFile(stream); // generate apple options file with custom fonts option allowed
        }

        private void AddAppleOptionsFile(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            ZipEntry metaDataFile = _zipFactory.MakeFileEntry(@"META-INF/com.apple.ibooks.display-options.xml", false);
            stream.PutNextEntry(metaDataFile);
            _appleOptionsFile.Write(stream);
            stream.CloseEntry();
        }

        private void AddMetaDataFile(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            ZipEntry metaDataFile = _zipFactory.MakeFileEntry(@"META-INF/container.xml",false);
            stream.PutNextEntry(metaDataFile);
            ContainerFile container = new ContainerFile(MakeFilePath("Content.opf"));
            container.Write(stream);
            stream.CloseEntry();
        }


        private void AddMetaDataFolder(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            ZipEntry entry = _zipFactory.MakeDirectoryEntry("META-INF", false);
            stream.PutNextEntry(entry);
            stream.CloseEntry();
        }

        private void AddBookData(ZipOutputStream stream)
        {
            AddBookFolder(stream);
            if (InjectLKRLicense)
            {
                AddLicenseFile(stream);
            }
            AddImages(stream);
            AddFontFiles(stream);
            AddFiles(stream);
            AddTOCFile(stream);
            AddContentFile(stream);
        }

        private void AddLicenseFile(ZipOutputStream stream)
        {
            const string fileName = "license.xhtml";
            // for test let's just create one file
            stream.SetLevel(9);
            ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(string.Format(@"license/{0}", fileName)), false);
            stream.PutNextEntry(file);


            LicenseFile licensePage = new LicenseFile { FlatStructure = FlatStructure, EmbedStyles = EmbedStyles };
            licensePage.Create();
            licensePage.Write(stream);
            _content.AddTOC(FlatStructure ? fileName : string.Format("license/{0}", fileName) , "license");
           
        }

        private void AddFontFiles(ZipOutputStream stream)
        {
            if (_fontSettings.NumberOfEmbededFiles > 0)
            {
                AddFontsFolder(stream);
                stream.SetLevel(9);
                foreach (var embededFileLocation in _fontSettings.EmbededFilesLocations)
                {
                    string filePath = MakeFilePath(string.Format(@"fonts/{0}", Path.GetFileName(embededFileLocation)));
                    ZipEntry entry = _zipFactory.MakeFileEntry(filePath, false);
                    stream.PutNextEntry(entry);
                    try
                    {
                        using (var reader = new BinaryReader(File.OpenRead(embededFileLocation)))
                        {
                            int iCount;
                            Byte[] buffer = new Byte[2048];
                            while ((iCount = reader.Read(buffer, 0, 2048)) != 0)
                            {
                                stream.Write(buffer, 0, iCount);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.log.ErrorFormat("Error loading font file {0} : {1}", embededFileLocation, ex.ToString());
                        continue;
                    }
                    _content.AddFontFile(string.Format(FlatStructure ? "{0}" : @"fonts/{0}", Path.GetFileName(embededFileLocation)),
                                        Path.GetFileNameWithoutExtension(embededFileLocation), _fontSettings.GetMediaType(embededFileLocation));
                }
            }
        }

        private void AddFontsFolder(ZipOutputStream stream)
        {
            CreateZipFolder(stream, "fonts");
        }

        private void CreateZipFolder(ZipOutputStream stream, string folder)
        {
            if (FlatStructure)
            {
                return;
            }
            stream.SetLevel(9);
            ZipEntry entry = _zipFactory.MakeDirectoryEntry(string.Format(@"OEBPS/{0}",folder), false);
            stream.PutNextEntry(entry);
            stream.CloseEntry();
        }

        private void AddFiles(ZipOutputStream stream)
        {
            AddAdobeTemplate(stream);
            AddCSSFiles(stream);
            AddCover(stream);
            AddTitle(stream);
            AddAnnotation(stream);
            AddBookContent(stream);
            if (_aboutTexts.Count >0 || _aboutLinks.Count > 0)
            {
                AddAbout(stream);                
            }
        }

        private void AddAdobeTemplate(ZipOutputStream stream)
        {
            if (!UseAdobeTemplate || string.IsNullOrEmpty(AdobeTemplatePath))
            {
                return;
            }
            CreateZipFolder(stream, "Template");
            stream.SetLevel(9);
            _adobeTemplate.TemplateFileInputPath = AdobeTemplatePath;
            try
            {
                _adobeTemplate.Load();
                string fileNameFormat = MakeFilePath(string.Format(@"Template/{0}", _adobeTemplate.TemplateFileOutputName)); 
                ZipEntry templateFile = _zipFactory.MakeFileEntry(fileNameFormat, false);
                stream.PutNextEntry(templateFile);
                _adobeTemplate.Write(stream);
                _content.AddXPGTTemplate(fileNameFormat, _adobeTemplate.TemplateFileOutputName);
            }
            catch (Exception)
            {             
                Logger.log.ErrorFormat("Exception adding template, template will not be added");
            }
        }

        private void AddAnnotation(ZipOutputStream stream)
        {
            if (AnnotationPage != null)
            {

                // for test let's just create one file
                stream.SetLevel(9);
                ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(AnnotationPage.FileName), false);
                stream.PutNextEntry(file);


                AnnotationPage.FlatStructure = FlatStructure;
                AnnotationPage.EmbedStyles = EmbedStyles;
                AnnotationPage.StyleFiles.Add(_mainCss);
                if (UseAdobeTemplate)
                {
                    AnnotationPage.StyleFiles.Add(_adobeTemplate);
                }

                AnnotationPage.Write(stream);

                const string fileID = "annotation";
                _content.AddXHTMLTextItem(AnnotationPage.FileName, fileID, AnnotationPage.DocumentType);

            }
        }

        private void AddCSSFiles(ZipOutputStream stream)
        {
            foreach (var cssFile in CSSFiles)
            {
                _mainCss.Load(cssFile.FileExtPath, true);
            }
            if (EmbedStyles)
            {
                return;
            }
            CreateZipFolder(stream, _mainCss.LocationSubFolder);
            AddMainCSS(stream);
        }


        private void AddMainCSS(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            string filename = MakeFilePath(_mainCss.InternalPath);
            ZipEntry file = _zipFactory.MakeFileEntry(filename, false);
            stream.PutNextEntry(file);
            _mainCss.Write(stream);

            _content.AddCSS(_mainCss.InternalPath, _mainCss.ID);
        }

        private void AddTitle(ZipOutputStream stream)
        {
            if (TitlePage != null)
            {
                TitlePage.FlatStructure = FlatStructure;
                TitlePage.EmbedStyles = EmbedStyles;

                // for test let's just create one file
                stream.SetLevel(9);
                ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(TitlePage.FileName), false);
                stream.PutNextEntry(file);


                TitlePage.StyleFiles.Add(_mainCss);
                if (UseAdobeTemplate)
                {
                    TitlePage.StyleFiles.Add(_adobeTemplate);
                }

                TitlePage.Write(stream);

                const string fileID = "title";
                _content.AddXHTMLTextItem(TitlePage.FileName, fileID, TitlePage.DocumentType);

            }

        }

        private void AddAbout(ZipOutputStream stream)
        {
            const string fileName = "about.xhtml";
            // for test let's just create one file
            stream.SetLevel(9);
            ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(fileName),false);
            stream.PutNextEntry(file);


            AboutPageFile aboutPage = new AboutPageFile{ FlatStructure = FlatStructure, EmbedStyles = EmbedStyles,AboutLinks = _aboutLinks, AboutTexts = _aboutTexts};
            aboutPage.Create();
            aboutPage.StyleFiles.Add(_mainCss);
            if (UseAdobeTemplate)
            {
                aboutPage.StyleFiles.Add(_adobeTemplate);
            }

            aboutPage.Write(stream);

            const string fileID = "about";

            _content.AddXHTMLTextItem(fileName, fileID, aboutPage.DocumentType);
        }

        /// <summary>
        /// Writes book content to the stream
        /// </summary>
        /// <param name="stream">stream to write to</param>
        private void AddBookContent(ZipOutputStream stream)
        {
            int count = 1;

            stream.SetLevel(9);

            foreach (var section in _sections)
            {
                section.FlatStructure = FlatStructure;
                section.EmbedStyles = EmbedStyles;
                if (string.IsNullOrEmpty(section.FileName)) // if file name not defined yet create our own (not converter case)
                {
                    section.FileName = string.Format("section{0}.xhtml", count);                    
                }
                XDocument document = section.Generate();
                long docSize = document.EstimateSize();
                if ( docSize >= BookDocument.MaxSize )
                {
                    // This case is not for converter
                    // after converter the files should be in right size already
                    int subCount = 0;
                    foreach( var subsection in section.Split() )
                    {
                        subsection.FlatStructure = FlatStructure;
                        subsection.EmbedStyles = EmbedStyles;
                        subsection.FileName = string.Format("{0}_{1}.xhtml", Path.GetFileNameWithoutExtension(section.FileName), subCount);
                        ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(subsection.FileName), false);
                        stream.PutNextEntry(file);
                        document = subsection.Generate();
                        WriteDocumentToStream(document,stream);
                        AddBookContentSection(subsection,count,subCount);
                        subCount++;
                    }
                    count++;                        
                }
                else
                {
                    ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(section.FileName), false);
                    stream.PutNextEntry(file);
                    WriteDocumentToStream(document, stream);
                    AddBookContentSection(section, count,0);
                    count++;                    
                }
                
            }

            // remove navigation leaf end points with empty names
            _tableOfContentFile.Consolidate();

            // to be valid we need at least one NAVPoint
            if ( _tableOfContentFile.IsNavMapEmpty() && (_sections.Count > 0) )
            {
                _tableOfContentFile.AddNavPoint(_sections[0].FileName, _rule.Translate(_title.BookTitles[0].TitleName, TranliterateToc ? TranslitMode : TranslitModeEnum.None));                        
            }
        }

        private void WriteDocumentToStream(XDocument document, ZipOutputStream stream)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.CloseOutput = false;
            settings.Encoding = Encoding.UTF8;
            settings.Indent = true;

            using (var writer = XmlWriter.Create(stream, settings))
            {
                document.WriteTo(writer);
            }            
        }

        private void AddBookContentSection(BookDocument subsection, int count,int subcount)
        {
            string fileId = string.Format("bookcontent{0}_{1}", count,subcount); // generate unique ID
            _content.AddXHTMLTextItem(subsection.FileName, fileId, subsection.DocumentType);
            if (!subsection.NotPartOfNavigation)
            {
                if (subsection.NavigationLevel <= 1)
                {
                    _tableOfContentFile.AddNavPoint(subsection.FileName, _rule.Translate(subsection.PageTitle, TranliterateToc? TranslitMode : TranslitModeEnum.None));
                }
                else
                {
                    _tableOfContentFile.AddSubNavPoint(subsection.NavigationParent.FileName, subsection.FileName, _rule.Translate(subsection.PageTitle, TranliterateToc ? TranslitMode : TranslitModeEnum.None));
                }
            }
        }



        private void AddCover(ZipOutputStream stream)
        {
            if (string.IsNullOrEmpty(_coverImage) )
            {
                // if no cover image - no cover
                return;
            }
            EPUBImage eImage;
            // also image need to be in list of the images we have (check in case of invalid input)
            if (!_images.TryGetValue(_coverImage, out eImage))
            {
                return;
            }
            string  fileName = "cover.xhtml";
            // for test let's just create one file
            stream.SetLevel(9);
            ZipEntry file = _zipFactory.MakeFileEntry(MakeFilePath(fileName),false);
            stream.PutNextEntry(file);

            CoverPageFile cover = new CoverPageFile
            {
                CoverFileName = GetCoverImageName(eImage),
                FlatStructure = FlatStructure,
                EmbedStyles = EmbedStyles
            };
            cover.StyleFiles.Add(_mainCss);
            if (UseAdobeTemplate)
            {
                cover.StyleFiles.Add(_adobeTemplate);                
            }



            cover.Write(stream);

            if (!string.IsNullOrEmpty(eImage.ID))
            {
                _content.CoverId = eImage.ID;                
            }


            const string fileID = "cover";

            _content.AddXHTMLTextItem(fileName, fileID, cover.DocumentType);
        }

        private string GetCoverImageName(EPUBImage eImage)
        {
            int count = 0;
            foreach (var image in _images)
            {
                if (image.Key == _coverImage)
                {
                    return FlatStructure ? eImage.ID : string.Format("images/{0}",eImage.ID);
                }
                count++;
            }
            return FlatStructure ? "cover.jpg" : "images/cover.jpg";
        }

        private void AddTOCFile(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            ZipEntry tocFile = _zipFactory.MakeFileEntry(MakeFilePath("toc.ncx"), false);
            stream.PutNextEntry(tocFile);
            _tableOfContentFile.ID = _title.Identifiers[0].ID;
            _tableOfContentFile.Title = _rule.Translate(_title.BookTitles[0].TitleName,TranslitMode);
            _tableOfContentFile.Write(stream);
            _content.AddTOC("toc.ncx","ncx");
        }



        private void AddContentFile(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            ZipEntry contentFile = _zipFactory.MakeFileEntry(MakeFilePath("Content.opf"), false);
            stream.PutNextEntry(contentFile);
            _content.Title = _title;
            _content.Write(stream);
        }

        private string MakeFilePath(string fileName)
        {
            if (FlatStructure)
            {
                return Path.GetFileName(fileName);
            }
            return string.Format(@"OEBPS/{0}", fileName);
        }




        private void AddImages(ZipOutputStream stream)
        {
            if (_images.Count > 0)
            {
                AddImagesFolder(stream);
                AddImagesFiles(stream);               
            }
        }

        private void AddImagesFiles(ZipOutputStream stream)
        {
            stream.SetLevel(9);
            //int number = 0;
            foreach (var epubImage in _images)
            {
                string filename = epubImage.Value.ID;
                string filePath = MakeFilePath(string.Format(@"images/{0}", filename));
                ZipEntry entry = _zipFactory.MakeFileEntry(filePath, false);
                stream.PutNextEntry(entry);
                stream.Write(epubImage.Value.ImageData, 0, epubImage.Value.ImageData.Length);
                _content.AddImage(string.Format(FlatStructure ? "{0}" : @"images/{0}", filename), epubImage.Value.ID, epubImage.Value.ImageType);
            }

        }

        private void AddImagesFolder(ZipOutputStream stream)
        {
            CreateZipFolder(stream, "images");
        }

        private void AddBookFolder(ZipOutputStream stream)
        {
            if (FlatStructure)
            {
                return;
            }
            stream.SetLevel(9);
            ZipEntry entry = _zipFactory.MakeDirectoryEntry("OEBPS", false);
            stream.PutNextEntry(entry);
            stream.CloseEntry();
        }

        private void AddMimeTypeEntry(ZipOutputStream s)
        {
            s.SetLevel(0);
            ZipEntry entry = _zipFactory.MakeFileEntry("mimetype",false);
            entry.CompressionMethod = CompressionMethod.Stored;
            entry.IsUnicodeText = false;
            entry.ZipFileIndex = 0;
            s.PutNextEntry(entry);
            const string mimetype = "application/epub+zip";
            ASCIIEncoding encoding = new ASCIIEncoding();
            byte[] buffer = encoding.GetBytes(mimetype);
            s.Write(buffer, 0, buffer.Length);
            s.CloseEntry();
        }


        public void AddCoverImage(string imageRef)
        {
            _coverImage = imageRef;            
        }

        public  BookDocument AddDocument(string ID)
        {
            BookDocument section = new BookDocument {PageTitle = ID};
            section.StyleFiles.Add(_mainCss);
            if (UseAdobeTemplate)
            {
                section.StyleFiles.Add(_adobeTemplate);
            }

            _sections.Add(section);
            return section;
        }


        public void SetEPubFonts(EPubFontSettings fonts, string resourcesPath, bool decorateFontNames)
        {
            _fontSettings.ResourceMask = resourcesPath;
            _fontSettings.Load(fonts,decorateFontNames?Title.Identifiers[0].IdentifierName:string.Empty);

            AddFontsToCSS(_fontSettings.Fonts);
            AddCssElementsToCSS(_fontSettings.CssElements);
        }

        private void AddCssElementsToCSS(Dictionary<string, Dictionary<string, List<CSSFontFamily>>> cssElements)
        {
            // Now add the elements
            foreach (var elementName in cssElements.Keys)
            {
                foreach (var elementClass in cssElements[elementName].Keys)
                {
                    BaseCSSItem cssItem = new BaseCSSItem();
                    StringBuilder sb = new StringBuilder();
                    sb.Append(elementName);
                    if (!string.IsNullOrEmpty(elementClass))
                    {
                        sb.AppendFormat(".{0}",elementClass);
                    }
                    cssItem.Name = sb.ToString();

                    // now build a list of fonts
                    sb.Clear();
                    int counter = 0;
                    foreach (var fontFamily in cssElements[elementName][elementClass])
                    {
                        sb.AppendFormat("\"{0}\"",fontFamily.Name);
                        if (counter!= 0)
                        {
                            sb.Append(", ");
                        }
                        counter++;
                    }
                    cssItem.Parameters.Add("font-family", sb.ToString());
                    _mainCss.AddTarget(cssItem);
                }
            }
        }


        private void AddFontsToCSS(Dictionary<string, CSSFontFamily> fontsFamilies)
        {
            // Add the fonts to CSS
            foreach (var cssFontFamily in fontsFamilies)
            {
                foreach (var subFont in cssFontFamily.Value.Fonts)
                {
                    CssFontDefinition cssFont = new CssFontDefinition();
                    cssFont.Family = cssFontFamily.Key;
                    cssFont.FontStyle = CssFontDefinition.FromStyle(subFont.FontStyle);
                    cssFont.FontWidth = CssFontDefinition.FromWidth(subFont.FontWidth);
                    List<string> sources = new List<string>();
                    foreach (var fontSource in subFont.Sources)
                    {
                        sources.Add(CssFontDefinition.ConvertToSourceString(fontSource, EmbedStyles, FlatStructure));
                    }
                    cssFont.FontSrcs = sources;
                    _mainCss.AddFont(cssFont);
                }
            }
        }
    }

    public static class XHTMLExtensions
    {
        public static bool IsIdPresent(this IXHTMLItem iXHTMLItem, string value)
        {
            if (iXHTMLItem.SubElements() == null)
            {
                return false;
            }
            foreach (var s in iXHTMLItem.SubElements())
            {
                if (s is Anchor)
                {
                    Anchor a = s as Anchor;
                    if (a.ID != null)
                    {
                        if (a.ID.Value == value)
                        {
                            return true;
                        }
                    }
                }
                else 
                {
                    if( IsIdPresent(s,value) )
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
