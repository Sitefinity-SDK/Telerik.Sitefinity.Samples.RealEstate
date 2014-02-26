﻿using System.Linq;
using System.IO;
using System.Text;
using System;
using Telerik.Sitefinity;
using Telerik.Sitefinity.Utilities.Zip;
using Telerik.Sitefinity.Modules.Pages;
using Telerik.Sitefinity.Pages.Model;
using Telerik.Sitefinity.Modules.GenericContent.Web.UI;
using Telerik.Sitefinity.Web.UI;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Web.Configuration;
using System.Xml.Serialization;
using Telerik.Sitefinity.Web.UI.PublicControls;
using Telerik.Sitefinity.Modules.Libraries;
using Telerik.Sitefinity.GenericContent.Model;
using Telerik.Sitefinity.Web.UI.NavigationControls;
using System.Globalization;
using Telerik.Sitefinity.Modules.Pages.Configuration;
using Telerik.Sitefinity.Abstractions;

namespace TemplateImporter
{
    public class TemplateImporter
    {

        private string sitefinityTemplatesInstallationFolder;
        private string templateExtractionFolder;
        private string zipFileName;
        private string applicationPath;

        private PageTemplate pageTemplate;
        private PageManager pageManager;
        private Template templateobject;



        /// <summary>
        /// Initializes a new instance of the <see cref="TemplateImporter" /> class.
        /// </summary>
        /// <param name="ZipFileName">Name of the zip file.</param>
        /// <param name="applicationPath">The application path.</param>
        public TemplateImporter(string ZipFileName, string applicationPath)
        {
            string uniqueExtension = DateTime.Now.ToFileTime().ToString();

            this.applicationPath = applicationPath;
            this.zipFileName = ZipFileName;

            this.templateExtractionFolder = string.Concat(applicationPath, "App_Data\\temp_", uniqueExtension, "\\");
            this.sitefinityTemplatesInstallationFolder = string.Concat(applicationPath, "App_Data\\Sitefinity\\WebsiteTemplates\\");

            pageManager = PageManager.GetManager();
            pageTemplate = pageManager.CreateTemplate();
            pageTemplate.Category = new Guid();
        }


        /// <summary>
        /// Imports the uploaded template in the SF backend and registers it.
        /// </summary>
        public bool Import()
        {
            bool success = true;
            string fileToExtract = string.Concat(applicationPath, zipFileName);

            try
            {

                Extract(fileToExtract, templateExtractionFolder);

                GetTemplateFromXML();

                if (templateobject != null)
                {

                    //set template title
                    if (templateobject.metadata == null || templateobject.metadata.metadataitems == null)
                    {
                        pageTemplate.Name = "untitled";
                        pageTemplate.Title = "untitled";
                    }
                    else
                    {
                        string title = templateobject.metadata.metadataitems.Where(m => m.id == "title").First().value;

                        pageTemplate.Name = title;
                        pageTemplate.Title = title;
                    }

                    string templateInstallationFolder = string.Concat(sitefinityTemplatesInstallationFolder, pageTemplate.Name);
                    CreateTemplateFolderStructure(templateInstallationFolder, pageTemplate.Name);


                    string cssPath = string.Concat(templateExtractionFolder, "css\\");
                    string imageSource = string.Concat(templateExtractionFolder, "images\\");
                    string masterPagePath = string.Concat(templateExtractionFolder, "page.master");

                    string themeTargetFolder = string.Concat(templateInstallationFolder, "\\App_Themes\\", pageTemplate.Name);
                    string imagesTargetFolder = string.Concat(themeTargetFolder, "\\Images");
                    string cssTargetFolder = string.Concat(themeTargetFolder, "\\Global\\");

                    if (File.Exists(masterPagePath))
                    {

                        File.Copy(masterPagePath, string.Concat(templateInstallationFolder, "\\App_Master\\page.master"), true);
                    }

                    if (Directory.Exists(imageSource))
                        CopyFiles(imageSource, imagesTargetFolder);

                    if (Directory.Exists(cssPath))
                    {

                        CopyFiles(cssPath, cssTargetFolder);
                        RegisterTheme();
                    }


                    UploadImages(imagesTargetFolder, pageTemplate.Name);

                    if (templateobject.layout != null)
                    {
                        RegisterTemplate();
                    }
                }
                else
                {
                    success = false;
                }
            }
            catch (Exception ex)
            {
                success = false;
            }
            finally
            {
                File.Delete(fileToExtract);
                DeleteTemporaryFolder(templateExtractionFolder);

            }
            return success;
        }

        /// <summary>
        /// Deserializes the XML layout as a template object.
        /// </summary>
        private void GetTemplateFromXML()
        {
            string layoutFilePath = string.Concat(templateExtractionFolder, "layout.xml");

            XmlSerializer serializer = new XmlSerializer(typeof(Template));
            StreamReader reader = new StreamReader(layoutFilePath);

            try
            {
                object obj = serializer.Deserialize(reader);
                templateobject = (Template)obj;
                reader.Close();
            }
            catch (Exception ex)
            {

            }
        }


        /// <summary>
        /// Extracts the specified zip file name to the specified directory.
        /// </summary>
        /// <param name="zipFileArchive">Name of the zip file.</param>
        /// <param name="directory">The directory.</param>
        private void Extract(string zipFileArchive, string directory)
        {
            try
            {
                Directory.CreateDirectory(directory);
                using (ZipFile zip = ZipFile.Read(zipFileArchive))
                {

                    foreach (var file in zip.Entries)
                    {
                        file.Extract(directory, true);
                    }

                }
            }
            catch (System.Exception ex)
            {
            }
        }


        /// <summary>
        /// Creates the template folder structure.
        /// </summary>
        private void CreateTemplateFolderStructure(string templateRootDirecotry, string templateName)
        {
            Directory.CreateDirectory(templateRootDirecotry);
            Directory.CreateDirectory(string.Concat(templateRootDirecotry, "\\App_Master"));
            Directory.CreateDirectory(string.Concat(templateRootDirecotry, "\\App_Themes\\", templateName, "\\Global"));
            Directory.CreateDirectory(string.Concat(templateRootDirecotry, "\\App_Themes\\", templateName, "\\Images"));
        }



        /// <summary>
        /// Copies files
        /// </summary>
        /// <param name="sourcePath">The source path.</param>
        /// <param name="targetPath">The target path.</param>
        private void CopyFiles(string sourcePath, string targetPath)
        {
            string[] files = Directory.GetFiles(sourcePath);

            // Copy the files and overwrite destination files if they already exist.
            foreach (string s in files)
            {
                // Use static Path methods to extract only the file name from the path.
                string fileName = System.IO.Path.GetFileName(s);
                string destFile = System.IO.Path.Combine(targetPath, fileName);
                System.IO.File.Copy(s, destFile, true);
            }
        }


        /// <summary>
        /// Registers the theme.
        /// </summary>
        private void RegisterTheme()
        {
            pageTemplate.Theme = pageTemplate.Name;

            ConfigManager manager = Config.GetManager();
            var appearanceConfig = manager.GetSection<AppearanceConfig>();
            var defaultSamplesTheme = new ThemeElement(appearanceConfig.FrontendThemes)
            {
                Name = pageTemplate.Theme,
                Path = string.Concat("~/App_Data/Sitefinity/WebsiteTemplates/", pageTemplate.Name, "/App_Themes/", pageTemplate.Name)
            };

            if (!appearanceConfig.FrontendThemes.ContainsKey(defaultSamplesTheme.Name))
                appearanceConfig.FrontendThemes.Add(defaultSamplesTheme);

            appearanceConfig.DefaultFrontendTheme = pageTemplate.Theme;

            manager.SaveSection(appearanceConfig);
        }

        /// <summary>
        /// Registers the template.
        /// </summary>
        private void RegisterTemplate()
        {

            var present = this.pageManager.CreatePresentationItem<TemplatePresentation>();
            present.DataType = Presentation.HtmlDocument;
            present.Name = "master";
            var resName = "Telerik.Sitefinity.Resources.Pages.Frontend.aspx";
            present.Data = ControlUtilities.GetTextResource(resName, Config.Get<ControlsConfig>().ResourcesAssemblyInfo);

            pageTemplate.MasterPage = string.Concat("~/App_Data/Sitefinity/WebsiteTemplates/", pageTemplate.Name, "/App_Master/page.master");


            var sibling = new Guid();

            for (int i = 0; i < templateobject.layout.placeholders.Length; i++)
            {
                var placeholder = templateobject.layout.placeholders[i];

                //var layout = new LayoutControl();

                //var ctrlData = this.pageManager.CreateControl<TemplateControl>();


                //if (placeholder.layoutwidget != null)
                //{
                //    layout.Layout = createPlaceholderLayout(placeholder.layoutwidget.columns, placeholder.id);

                //}

                //ctrlData.ObjectType = layout.GetType().FullName;
                //ctrlData.PlaceHolder = "Body";
                //ctrlData.SiblingId = sibling;
                //sibling = ctrlData.Id;
                //ctrlData.Caption = placeholder.id;

                //this.pageManager.ReadProperties(layout, ctrlData);
                //this.pageManager.SetControlId(pageTemplate, ctrlData);


                for (int j = 0; j < placeholder.layoutwidget.columns.Length; j++)
                {

                    var column = placeholder.layoutwidget.columns[j];

                    var widget = column.widget;
                    if (widget.type != null)
                    {
                        ControlData ctrlData = null;
                        if (widget.type.ToLower() == "content block")
                        {
                            ContentBlockBase newContentBlock = new ContentBlockBase();
                            newContentBlock.Html = widget.properties.text;
                            newContentBlock.CssClass = widget.cssclass;
                            newContentBlock.LayoutTemplatePath = "~/SFRes/Telerik.Sitefinity.Resources.Templates.Backend.GenericContent.ContentBlock.ascx";

                            var templateContentBlock = pageManager.CreateControl<Telerik.Sitefinity.Pages.Model.TemplateControl>(newContentBlock, widget.sfID);
                            templateContentBlock.Caption = "Content Block";

                            pageTemplate.Controls.Add(templateContentBlock);
                            ctrlData = templateContentBlock;
                        }
                        else if (widget.type.ToLower() == "image")
                        {
                            ImageControl newImage = new ImageControl();
                            newImage.LayoutTemplatePath = "~/SFRes/Telerik.Sitefinity.Resources.Templates.PublicControls.ImageControl.ascx";
                            newImage.CssClass = widget.cssclass;
                            newImage.ImageId = GetImageId(widget.properties.filename, pageTemplate.Name);

                            var templateImageControl = pageManager.CreateControl<Telerik.Sitefinity.Pages.Model.TemplateControl>(newImage, widget.sfID);
                            templateImageControl.Caption = "Image";

                            pageTemplate.Controls.Add(templateImageControl);
                            ctrlData = templateImageControl;

                        }
                        else if (widget.type.ToLower() == "navigation")
                        {

                            string type = widget.properties.navigationtype;
                            NavigationControl navigation = new NavigationControl();

                            navigation.SelectionMode = PageSelectionModes.TopLevelPages;
                            NavigationModes navigationMode;

                            switch (type)
                            {
                                case "horizontalcontrol": navigationMode = NavigationModes.HorizontalSimple;
                                    break;

                                case "horizontal2levelscontrol": navigationMode = NavigationModes.HorizontalDropDownMenu;
                                    break;

                                case "tabscontrol": navigationMode = NavigationModes.HorizontalTabs;
                                    break;

                                case "verticalcontrol": navigationMode = NavigationModes.VerticalSimple;
                                    break;

                                case "treeviewcontrol": navigationMode = NavigationModes.VerticalTree;
                                    break;

                                case "sitemapcontrol": navigationMode = NavigationModes.SiteMapInColumns;
                                    break;

                                default: navigationMode = NavigationModes.HorizontalSimple;
                                    break;
                            }

                            navigation.NavigationMode = navigationMode;
                            navigation.Skin = widget.cssclass;

                            var templateNavigationControl = pageManager.CreateControl<Telerik.Sitefinity.Pages.Model.TemplateControl>(navigation, widget.sfID);
                            templateNavigationControl.Caption = "Navigation";

                            pageTemplate.Controls.Add(templateNavigationControl);
                            ctrlData = templateNavigationControl;

                        }

                        var widgetCulture = this.GetCurrentLanguage();
                        pageManager.SetControlId(pageTemplate, ctrlData, widgetCulture);
                    }
                }

                //pageTemplate.Controls.Add(ctrlData);
            }

            pageTemplate.Category = Telerik.Sitefinity.Abstractions.SiteInitializer.CustomTemplatesCategoryId;
            pageManager.SaveChanges();

            // publish the template
            var draft = pageManager.EditTemplate(pageTemplate.Id);
            var master = pageManager.TemplatesLifecycle.CheckOut(draft);
            master = pageManager.TemplatesLifecycle.CheckIn(master);
            pageManager.TemplatesLifecycle.Publish(master);
            pageManager.SaveChanges();

        }

        protected CultureInfo GetCurrentLanguage()
        {
            var enableWidgetTranslations = Config.Get<PagesConfig>().EnableWidgetTranslations;
            var widgetCulture = CultureInfo.CurrentUICulture;
            if (enableWidgetTranslations.HasValue == false || enableWidgetTranslations.Value == false || AppSettings.CurrentSettings.Multilingual == false)
            {
                widgetCulture = null;
            }

            return widgetCulture;
        }

        /// <summary>
        /// Deletes the temporary folder.
        /// </summary>
        /// <param name="FolderName">Name of the folder.</param>
        private void DeleteTemporaryFolder(string FolderName)
        {
            try
            {
                DirectoryInfo dir = new DirectoryInfo(FolderName);
                dir.Delete(true);
            }
            catch (Exception ex)
            {
            }
        }

        public static void UploadImages(string folderPath, string albumName)
        {
            var imagesCreated = App.WorkWith().Albums().Where(i => i.Title == albumName).Get().Count() > 0;
            if (!imagesCreated)
            {
                DirectoryInfo myFolder = new DirectoryInfo(folderPath);
                var album = App.WorkWith().Album().CreateNew().Do(a => { a.Title = albumName; a.Description = "Images imported from template"; }).SaveAndContinue();

                foreach (var file in myFolder.GetFiles())
                {
                    album.CreateImage()
                       .Do(image1 => { image1.Title = file.Name; image1.Description = file.Name; image1.UrlName = file.Name; })
                       .CheckOut().UploadContent(file.OpenRead(), file.Extension)
                       .CheckInAndPublish()
                       .SaveChanges();
                }
            }
        }

        public static Guid GetImageId(string imageName, string albumTitle)
        {
            var librariesManager = LibrariesManager.GetManager();
            Guid imageId = Guid.Empty;


            var album = librariesManager.GetAlbums().Where(a => a.Title.Equals(albumTitle)).FirstOrDefault();
            if (album == null)
                return imageId;
            var s = Path.GetFileName(imageName);
            var image = album.Images().Where(i => i.Title == Path.GetFileName(imageName) && i.Status == ContentLifecycleStatus.Live).FirstOrDefault();

            if (image != null)
            {
                imageId = image.Id;
            }

            return imageId;
        }

    }
}