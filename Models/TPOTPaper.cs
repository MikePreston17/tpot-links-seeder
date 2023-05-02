namespace dirty_tpot_links_seeder;

public class TPOTPaper
{
    public int id { get; set; } = -9999;

    public string Markdown { get; set; } = string.Empty;
    public string FrontMatter { get; set; } = string.Empty;


    public string Category { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string RawText { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public Dictionary<string, string> FrontmatterPairs { get; set; } = new Dictionary<string, string> ();
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string cover_image { get; set; } = string.Empty;
    public string template { get; set; } = string.Empty;
    public string comment_status { get; set; } = string.Empty;
    public string comments { get; set; } = string.Empty;
    public string custom { get; set; } = string.Empty;
    public string status { get; set; } = string.Empty;
    // public DateTime Modified { get; set; }
    // public DateTime Date { get; set; }
    public int author { get; set; } = -1;

}



public class FrontmatterPair 
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}


public class TPOTPapersResult
{
    public List<TPOTPaper> Papers { get; set; } = new List<TPOTPaper>();
    public Dictionary<string, string> Markdown { get; set; } = new Dictionary<string, string> ();
    public int Count { get; set; }
    public int valid_papers { get; set; }
    public int invalid_papers { get; set; }
    public double percent_passing => (1.0 * valid_papers / Count) * 100; 
    public int total_files_on_disk { get; set; }
    public string Elapsed { get; set; }
}

public class HugoPaper
{
    /**
      frontmatter = "---\n" + yaml + "---\n"
        text = frontmatter + "\n" + content
    */
    public string FrontMatter { get; set; } = string.Empty;

    public string RawMarkdown { get; set; } = string.Empty;
}


/*

---\nid: 13564\ntitle: Chat\nslug: chat-htm\nlink: https://www.thepathoftruth.com/chat-htm\ntype: page\nstatus: publish\ndate: '2016-08-21T02:07:48'\nmodified: '2017-02-12T14:26:59'\ncover_image: 0\nauthor: 10\ntags:\n- 1\ncomment_status: open\ntemplate: ''\nmeta: []\ncustom: []\nexcerpt: <p>Chat</p>\ncomments: []\n---


*/