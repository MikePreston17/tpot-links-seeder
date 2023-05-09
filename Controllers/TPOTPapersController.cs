using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.IO;
using CodeMechanic.Async;
using CodeMechanic.FileSystem;
using CodeMechanic.Regex;
using System.Linq;
using System.Reflection;
using NSpecifications;
using System.Text.RegularExpressions;
using CodeMechanic.Diagnostics;
using CodeMechanic.Reflection;
using CodeMechanic.Types;
using MySqlConnector;
using Insight.Database;
using PuppeteerSharp;
using CodeMechanic.PuppeteerExtensions;

namespace tpot_links_seeder.Controllers;

[ApiController]
[Route("[controller]")]
public partial class TPOTPaperController : ControllerBase
{
    private static readonly IDictionary<Type, ICollection<PropertyInfo>> _propertyCache =
        new Dictionary<Type, ICollection<PropertyInfo>>();

    private readonly ILogger<TPOTPaperController> logger;
    private readonly IWebHostEnvironment env;
    private readonly TPOTSettings settings;

    public TPOTPaperController(
        ILogger<TPOTPaperController> logs
        , IWebHostEnvironment environment_vars)
    {
        logger = logs;
        env = environment_vars;

        settings = new TPOTSettings()
            .With(setting =>
            {
                setting.Neo4jUri = "blarg";
                setting.Neo4jUser = "blarg";
                setting.Neo4jPassword = "blarg";
                setting.MySqlConnectionString = Environment.GetEnvironmentVariable("MYSQL_CONNECTIONSTRING");
            })
            .Dump("current settings"); // doesn't work on startup.  Who knew?
    }

    [HttpGet]
    public IActionResult Get()
    {
        return Ok();
    }

    [HttpGet(nameof(ExtractCSSSelectorsFromHtml))]
    public async Task<List<FacebookComment>> ExtractCSSSelectorsFromHtml()
    {
        return new FacebookComment().AsList();
    }

    [HttpGet(nameof(CreatePapersFromMarkdown))]
    public async Task<TPOTPapersResult> CreatePapersFromMarkdown(int limit = 15)
    {
        // var options = RegexOptions.Compiled
        //             | RegexOptions.IgnoreCase
        //             | RegexOptions.ExplicitCapture
        //             | RegexOptions.Multiline
        //             | RegexOptions.IgnorePatternWhitespace;

        string markdown_extraction_pattern = RegexPatterns.MarkdownExtractor.Last().Value;
        string hugo_paper_pattern = RegexPatterns.Hugos.Last().Value;
        string frontmatter_pair_pattern = RegexPatterns.FrontMatter.Last().Value;

        string root_folder = Path.Combine(env.ContentRootPath
                .GoUp(), "tpot_static_wip")
            .Dump("root");

        var watch = new Stopwatch();
        watch.Start();

        var queue = new SerialQueue();
        var tasks = new List<Task<TPOTPaper>>();

        var grepper = new Grepper()
            .With(grep =>
            {
                grep.RootPath = root_folder;
                grep.FileNamePattern = @".*\.md";
            });

        var all_files = grepper.GetFileNames() /*.Dump("Files")*/;

        var all_papers = all_files
                .Select(file_path => new TPOTPaper()
                    .With(p =>
                    {
                        p.FilePath = file_path;
                        p.RawText = System.IO.File.ReadAllText(file_path)
                            .Trim();
                    }))
                .Take(limit)
            ;

        var paper_properties = _propertyCache
            .TryGetProperties<TPOTPaper>(true)
            .ToArray();

        foreach (var paper in all_papers)
        {
            tasks.Add(queue.Enqueue(async () =>
            {
                HugoPaper hugo_paper = paper.RawText
                        .Extract<HugoPaper>(hugo_paper_pattern)
                        .FirstOrDefault()
                        .ToMaybe()
                        .Case(
                            some: hugo =>
                                hugo.With(h =>
                                {
                                    // This fixes the pesky escaped newline literals.
                                    h.FrontMatter = Regex.Replace(h.FrontMatter, @"\r\n?|\n", Environment.NewLine)
                                        .Trim();
                                    paper.Markdown = string.IsNullOrWhiteSpace(h.RawMarkdown)
                                        ? h.RawMarkdown
                                        : string.Empty;
                                }),
                            none: () => new HugoPaper()
                        )
                    ;

                paper.FrontMatter = hugo_paper?.FrontMatter?.Trim();

                paper.Markdown = !string.IsNullOrWhiteSpace(hugo_paper?.RawMarkdown)
                    ? hugo_paper?.RawMarkdown
                    : "";
                var pairs =
                        string.IsNullOrWhiteSpace(paper.FrontMatter)
                            ? new Dictionary<string, string>()
                            : paper.FrontMatter
                                // .Dump("raw frontmatter")
                                .Extract<FrontmatterPair>(frontmatter_pair_pattern)
                                .Dump("PAIRS")
                                .ToDictionary(
                                    p => p.Label,
                                    p => p.Value
                                )
                    ;

                paper.FrontmatterPairs = pairs;

                var matching_props = paper_properties
                    .Where(p => pairs.Keys //.Dump("keys")
                        .Contains(p.Name, StringComparer.OrdinalIgnoreCase));

                foreach (var prop in matching_props)
                {
                    Type prop_type = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

                    string name = prop.Name.ToLower();

                    object safe_value =
                        pairs[name] == null
                            ? null
                            : Convert.ChangeType(pairs[name], prop_type);

                    prop.SetValue(paper, safe_value, null);
                }

                return paper;
            }));
        }

        var results = (await Task.WhenAll(tasks)).ToList();
        // results.Dump("RESULTS");

        var valid = new Spec<TPOTPaper>(paper =>
                paper.id > 0
            // && paper.Markdown.Length > 0
        );

        var invalid = !valid;

        var passing = results
                .Where(valid)
                .ToList()
            // .Dump("passing")
            ;

        var non_passing = results
            .Where(invalid)
            .ToList();

        non_passing
            .FirstOrDefault()
            .Dump("bad egg");

        var response = new TPOTPapersResult()
            .With(res =>
            {
                res.Papers = results;
                res.Elapsed = watch.Elapsed.ToString();
                res.Count = results.Count;
                res.valid_papers = passing.Count;
                res.invalid_papers = non_passing.Count;
                res.total_files_on_disk = all_files.ToList().Count;
            });

        return response /*.Dump("RESPONSE")*/;
    }

    [HttpPost(nameof(StoreNewPaper))]
    public async Task<IEnumerable<TPOTPaper>> StoreNewPaper([FromBody] TPOTPaper incoming_paper)
    {
        settings.Dump("current settings");

        string connection_string = settings.MySqlConnectionString;

        using (MySqlConnection connection = new MySqlConnection(connection_string))
        {
            string query = """select * from railway.TPOTPapers;""";

            var results = connection
                .QuerySql<TPOTPaper>(query)
                .ToMaybe(); // I like maybe.  So sue me.

            return results
                .Case(
                    some: (papers) => papers?.Dump("results"),
                    none: () =>
                    {
                        "No results.  Sending back original paper".Dump();
                        return incoming_paper.AsList();
                    }
                );
        }
    }

    // [GeneratedRegex("")]
    public class FacebookPost
    {
        public static FacebookPost CreateInstance(string url = "")
        {
            // url.Dump();
            FacebookPostPattern.Dump("PATTERN");
            var instance = url.Extract<FacebookPost>(FacebookPostPattern)
                .SingleOrDefault()
                .Dump("fb post");
            return instance;
        }

        // https://regex101.com/r/vV12pT/1
        public const string FacebookPostPattern =
            @"^https?:\/\/www\.facebook\.com\/(?<Name>\w+)(\/posts\/)(?<post_id>[\w\d]+)\?(?<comment_ids>.*)";

        public string OutputPath => ToString();

        public string Extension { get; set; } = ".png";
        public string comment_ids { get; set; } = string.Empty;
        public string post_id { get; set; } = string.Empty;
        public string Name { get; set; }

        // https://www.facebook.com/officialbenshapiro/posts/pfbid0235H6HMsAdpGqULNzW4okjNxc5M31Fr6oof51GusMhMEtHq5tGMGoYdamG1JtHgbwl?comment_id=187601347521345&reply_comment_id=153781794119258&notif_id=1683167179141365&notif_t=comment_mention&ref=notif

        public override string ToString()
        {
            return $"{Name}_{post_id}.{Extension ?? ".png"}";
        }
    }

    [HttpGet(nameof(TakeScreenshot))]
    public async Task<FacebookPost> TakeScreenshot(
        // string url = "http://www.google.com"
        string url =
            "https://www.facebook.com/officialbenshapiro/posts/pfbid0235H6HMsAdpGqULNzW4okjNxc5M31Fr6oof51GusMhMEtHq5tGMGoYdamG1JtHgbwl?comment_id=187601347521345&reply_comment_id=153781794119258&notif_id=1683167179141365&notif_t=comment_mention&ref=notif"
        , string save_folder = "screenshots")
    {
        string output_folder = $"./{save_folder}";
        if (!Directory.Exists(output_folder))
            Directory.CreateDirectory(output_folder);

        var post = FacebookPost.CreateInstance(url)
            .Dump("post");

        string outfile_path = $"screenshots/{post.OutputPath}";
        using var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();
        await page.GoToAsync(url);
        await page.ScreenshotAsync(outfile_path);


        return post;
    }


    [HttpGet(nameof(SaveFullPageAsPDF))]
    public async Task<FacebookPost> SaveFullPageAsPDF(
        // string url = "http://www.google.com"
        string url =
            "https://www.facebook.com/officialbenshapiro/posts/pfbid0235H6HMsAdpGqULNzW4okjNxc5M31Fr6oof51GusMhMEtHq5tGMGoYdamG1JtHgbwl?comment_id=187601347521345&reply_comment_id=153781794119258&notif_id=1683167179141365&notif_t=comment_mention&ref=notif"
        , string save_folder = "pdf")
    {
        string output_folder = $"./{save_folder}";
        if (!Directory.Exists(output_folder))
            Directory.CreateDirectory(output_folder);

        var post = FacebookPost.CreateInstance(url)
            .Dump("post")
            .With(p => p.Extension = ".pdf");

        string output_file_path = $"screenshots/{post.OutputPath}".Dump("saving as:");
        using var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true
        });
        var page = await browser.NewPageAsync();
        await page.GoToAsync("http://www.google.com");
        await page.PdfAsync(output_file_path);

        return post;
    }

    [HttpGet(nameof(SaveFullPageAsHTML))]
    public async Task<FacebookPost> SaveFullPageAsHTML(
        // string url = "http://www.google.com"
        string url =
            "https://www.facebook.com/officialbenshapiro/posts/pfbid0235H6HMsAdpGqULNzW4okjNxc5M31Fr6oof51GusMhMEtHq5tGMGoYdamG1JtHgbwl?comment_id=187601347521345&reply_comment_id=153781794119258&notif_id=1683167179141365&notif_t=comment_mention&ref=notif"
        , string save_folder = "html")
    {
        string output_folder = $"./{save_folder}";
        if (!Directory.Exists(output_folder))
            Directory.CreateDirectory(output_folder);

        var post = FacebookPost.CreateInstance(url)
            .Dump("post")
            .With(p => p.Extension = ".html");

        string output_file_path = $"screenshots/{post.OutputPath}".Dump("saving as:");
        using var browserFetcher = new BrowserFetcher();
        await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);
        var options = new LaunchOptions
        {
            Headless = true
        };
        var browser = await Puppeteer.LaunchAsync(options);

        await browser.GetPropertyFromElement("h1");

        return post;
    }
}