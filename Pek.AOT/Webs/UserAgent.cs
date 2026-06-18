using System.Text.RegularExpressions;

using Pek.Caching;

namespace Pek.Webs;

/// <summary>UserAgent 解析器</summary>
public class UserAgent
{
    internal static readonly Dictionary<String, String> Platforms = new()
    {
        {"windows nt 10.0", "Windows 10"},
        {"windows nt 6.3", "Windows 8.1"},
        {"windows nt 6.2", "Windows 8"},
        {"windows nt 6.1", "Windows 7"},
        {"windows nt 6.0", "Windows Vista"},
        {"windows nt 5.2", "Windows 2003"},
        {"windows nt 5.1", "Windows XP"},
        {"windows nt 5.0", "Windows 2000"},
        {"windows nt 4.0", "Windows NT 4.0"},
        {"winnt4.0", "Windows NT 4.0"},
        {"winnt 4.0", "Windows NT"},
        {"winnt", "Windows NT"},
        {"windows 98", "Windows 98"},
        {"win98", "Windows 98"},
        {"windows 95", "Windows 95"},
        {"win95", "Windows 95"},
        {"windows phone", "Windows Phone"},
        {"windows", "Unknown Windows OS"},
        {"android", "Android"},
        {"blackberry", "BlackBerry"},
        {"iphone", "iOS"},
        {"ipad", "iOS"},
        {"ipod", "iOS"},
        {"os x", "Mac OS X"},
        {"ppc mac", "Power PC Mac"},
        {"freebsd", "FreeBSD"},
        {"ppc", "Macintosh"},
        {"linux", "Linux"},
        {"debian", "Debian"},
        {"sunos", "Sun Solaris"},
        {"beos", "BeOS"},
        {"apachebench", "ApacheBench"},
        {"aix", "AIX"},
        {"irix", "Irix"},
        {"osf", "DEC OSF"},
        {"hp-ux", "HP-UX"},
        {"netbsd", "NetBSD"},
        {"bsdi", "BSDi"},
        {"openbsd", "OpenBSD"},
        {"gnu", "GNU/Linux"},
        {"unix", "Unknown Unix OS"},
        {"symbian", "Symbian OS"},
    };

    internal static readonly Dictionary<String, String> Browsers = new()
    {
        {"OPR", "Opera"},
        {"Flock", "Flock"},
        {"Edge", "Spartan"},
        {"MQQ", "手机QQ浏览器"},
        {"QQ", "QQ浏览器"},
        {"MicroMessenger", "微信内置浏览器"},
        {"Baidu", "百度浏览器"},
        {"Chrome", "Chrome"},
        {"Opera.*?Version", "Opera"},
        {"Opera", "Opera"},
        {"MSIE", "Internet Explorer"},
        {"Internet Explorer", "Internet Explorer"},
        {"Trident.* rv" , "Internet Explorer"},
        {"Shiira", "Shiira"},
        {"Firefox", "Firefox"},
        {"Chimera", "Chimera"},
        {"Phoenix", "Phoenix"},
        {"Firebird", "Firebird"},
        {"Camino", "Camino"},
        {"Netscape", "Netscape"},
        {"OmniWeb", "OmniWeb"},
        {"Safari", "Safari"},
        {"Mozilla", "Mozilla"},
        {"Konqueror", "Konqueror"},
        {"icab", "iCab"},
        {"Lynx", "Lynx"},
        {"Links", "Links"},
        {"hotjava", "HotJava"},
        {"amaya", "Amaya"},
        {"IBrowse", "IBrowse"},
        {"Maxthon", "Maxthon"},
        {"Ubuntu", "Ubuntu Web Browser"},
    };

    internal static readonly Dictionary<String, String> Mobiles = new()
    {
        {"mobileexplorer", "Mobile Explorer"},
        {"palmsource", "Palm"},
        {"palmscape", "Palmscape"},
        {"motorola", "Motorola"},
        {"nokia", "Nokia"},
        {"palm", "Palm"},
        {"iphone", "Apple iPhone"},
        {"ipad", "iPad"},
        {"ipod", "Apple iPod Touch"},
        {"sony", "Sony Ericsson"},
        {"ericsson", "Sony Ericsson"},
        {"blackberry", "BlackBerry"},
        {"cocoon", "O2 Cocoon"},
        {"blazer", "Treo"},
        {"lg", "LG"},
        {"amoi", "Amoi"},
        {"xda", "XDA"},
        {"mda", "MDA"},
        {"vario", "Vario"},
        {"htc", "HTC"},
        {"samsung", "Samsung"},
        {"sharp", "Sharp"},
        {"sie-", "Siemens"},
        {"alcatel", "Alcatel"},
        {"benq", "BenQ"},
        {"ipaq", "HP iPaq"},
        {"mot-", "Motorola"},
        {"playstation portable", "PlayStation Portable"},
        {"playstation 3", "PlayStation 3"},
        {"playstation vita", "PlayStation Vita"},
        {"hiptop", "Danger Hiptop"},
        {"nec-", "NEC"},
        {"panasonic", "Panasonic"},
        {"philips", "Philips"},
        {"sagem", "Sagem"},
        {"sanyo", "Sanyo"},
        {"spv", "SPV"},
        {"zte", "ZTE"},
        {"sendo", "Sendo"},
        {"nintendo dsi", "Nintendo DSi"},
        {"nintendo ds", "Nintendo DS"},
        {"nintendo 3ds", "Nintendo 3DS"},
        {"wii", "Nintendo Wii"},
        {"open web", "Open Web"},
        {"openweb", "OpenWeb"},
        {"vivo", "Vivo"},
        {"oppo", "OPPO"},
        {"xiaomi", "小米"},
        {"miui", "小米"},
        {"SKR-", "小米黑鲨"},
        {"huawei", "华为"},
        {"HONOR", "华为荣耀"},
        {"ONEPLUS", "一加"},
        {"GM19", "一加"},
        {"Nexus", "Nexus"},
        {"ASUS", "ASUS"},
        {"android", "Android"},
        {"symbian", "Symbian"},
        {"SymbianOS", "SymbianOS"},
        {"elaine", "Palm"},
        {"series60", "Symbian S60"},
        {"windows ce", "Windows CE"},
        {"obigo", "Obigo"},
        {"netfront", "Netfront Browser"},
        {"openwave", "Openwave Browser"},
        {"mobilexplorer", "Mobile Explorer"},
        {"operamini", "Opera Mini"},
        {"opera mini", "Opera Mini"},
        {"opera mobi", "Opera Mobile"},
        {"fennec", "Firefox Mobile"},
        {"digital paths", "Digital Paths"},
        {"avantgo", "AvantGo"},
        {"xiino", "Xiino"},
        {"novarra", "Novarra Transcoder"},
        {"vodafone", "Vodafone"},
        {"docomo", "NTT DoCoMo"},
        {"o2", "O2"},
        {"mobile", "Generic Mobile"},
        {"wireless", "Generic Mobile"},
        {"j2me", "Generic Mobile"},
        {"midp", "Generic Mobile"},
        {"cldc", "Generic Mobile"},
        {"up.link", "Generic Mobile"},
        {"up.browser", "Generic Mobile"},
        {"smartphone", "Generic Mobile"},
        {"cellphone", "Generic Mobile"},
    };

    internal static readonly Dictionary<String, String> Robots = new()
    {
        {"googlebot", "Googlebot"},
        {"applebot", "AppleBot"},
        {"msnbot", "MSNBot"},
        {"dotbot", "DotBot"},
        {"360Spider", "360Spider"},
        {"baiduspider", "Baiduspider"},
        {"bingbot", "Bing"},
        {"slurp", "Inktomi Slurp"},
        {"yahoo", "Yahoo"},
        {"ask jeeves", "Ask Jeeves"},
        {"fastcrawler", "FastCrawler"},
        {"infoseek", "InfoSeek Robot 1.0"},
        {"lycos", "Lycos"},
        {"yandex", "YandexBot"},
        {"mediapartners-google", "MediaPartners Google"},
        {"CRAZYWEBCRAWLER", "Crazy Webcrawler"},
        {"adsbot-google", "AdsBot Google"},
        {"feedfetcher-google", "Feedfetcher Google"},
        {"curious george", "Curious George"},
        {"ia_archiver", "Alexa Crawler"},
        {"MJ12bot", "Majestic-12"},
        {"Uptimebot", "Uptimebot"},
        {"Sogou web spider", "Sogou Web Spider"},
        {"TelegramBot", "Telegram Bot"},
        {"DNSPod", "DNSPod"},
        {"SemrushBot", "SemrushBot"},
        {"AhrefsBot", "AhrefsBot"},
        {"BLEXBot", "BLEXBot"},
        {"YisouSpider", "YisouSpider"},
        {"Bytespider", "Bytespider"},
        {"PetalBot", "PetalBot"},
        {"DataForSeoBot", "DataForSeoBot"},
    };

    private readonly String _agent;

    /// <summary>是否为浏览器</summary>
    public Boolean IsBrowser { get; set; }

    /// <summary>是否为爬虫</summary>
    public Boolean IsRobot { get; set; }

    /// <summary>是否为移动设备</summary>
    public Boolean IsMobile { get; set; }

    /// <summary>平台</summary>
    public String Platform { get; set; } = "";

    /// <summary>浏览器</summary>
    public String Browser { get; set; } = "";

    /// <summary>浏览器版本</summary>
    public String BrowserVersion { get; set; } = "";

    /// <summary>移动设备</summary>
    public String Mobile { get; set; } = "";

    /// <summary>爬虫</summary>
    public String Robot { get; set; } = "";

    /// <summary>初始化一个<see cref="UserAgent"/>类型的实例</summary>
    /// <param name="userAgentString">UserAgent 字符串</param>
    internal UserAgent(String? userAgentString = null)
    {
        if (userAgentString != null)
        {
            _agent = userAgentString.Length > 512 ? userAgentString[..512] : userAgentString;
            SetPlatform();
            if (SetRobot()) return;
            if (SetBrowser()) return;
        }
    }

    internal Boolean SetPlatform()
    {
        foreach (var item in Platforms.Where(item => Regex.IsMatch(_agent, $"{Regex.Escape(item.Key)}", RegexOptions.IgnoreCase)))
        {
            Platform = item.Value;
            return true;
        }
        Platform = "Unknown Platform";
        return false;
    }

    internal Boolean SetBrowser()
    {
        foreach (var item in Browsers)
        {
            var match = Regex.Match(_agent, $@"{item.Key}.*?([0-9\.]+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                IsBrowser = true;
                BrowserVersion = match.Groups[1].Value;
                Browser = item.Value;
                SetMobile();
                return true;
            }
        }
        return false;
    }

    internal Boolean SetRobot()
    {
        foreach (var item in Robots.Where(item => Regex.IsMatch(_agent, $"{Regex.Escape(item.Key)}", RegexOptions.IgnoreCase)))
        {
            IsRobot = true;
            Robot = item.Value;
            SetMobile();
            return true;
        }

        return false;
    }

    internal Boolean SetMobile()
    {
        foreach (var item in Mobiles.Where(item => _agent.IndexOf(item.Key, StringComparison.OrdinalIgnoreCase) != -1))
        {
            IsMobile = true;
            Mobile = item.Value;
            return true;
        }

        return false;
    }

    /// <summary>返回表示当前对象的字符串</summary>
    /// <returns>UserAgent 字符串</returns>
    public override String ToString() => _agent;

    /// <summary>解析 UserAgent 字符串</summary>
    /// <param name="userAgentString">UserAgent 字符串</param>
    /// <returns>UserAgent 实例</returns>
    public static UserAgent Parse(String userAgentString)
    {
        return Cache.Default.GetOrAdd(userAgentString, entry => new UserAgent(entry), 3600);
    }
}
