namespace IcomRigControl.Services;

/// <summary>
/// Resolves a callsign to its DXCC entity (country) by prefix — the basis of DXCC
/// award tracking and "new one!" spot alerts. This is a CURATED prefix table covering
/// the common entities (not the full ~340); the exhaustive, exception-accurate source
/// is ARRL's cty.dat, and importing that is the noted refinement. Longest matching
/// prefix wins, so Hawaii (KH6) resolves before the generic US "K". Unknown prefixes
/// return "Unknown". See CLAUDE.md award tracking.
/// </summary>
public static class DxccResolver
{
    // prefix -> entity. Order doesn't matter; the longest match is chosen at lookup.
    private static readonly Dictionary<string, string> Prefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // North America
        ["K"] = "United States", ["W"] = "United States", ["N"] = "United States",
        ["AA"] = "United States", ["AB"] = "United States", ["AC"] = "United States",
        ["AD"] = "United States", ["AE"] = "United States", ["AF"] = "United States",
        ["AG"] = "United States", ["AI"] = "United States", ["AJ"] = "United States",
        ["AK"] = "United States",
        ["KH6"] = "Hawaii", ["KH7"] = "Hawaii", ["NH6"] = "Hawaii", ["WH6"] = "Hawaii", ["AH6"] = "Hawaii",
        ["KL"] = "Alaska", ["NL"] = "Alaska", ["WL"] = "Alaska", ["AL"] = "Alaska",
        ["KP4"] = "Puerto Rico", ["NP4"] = "Puerto Rico", ["WP4"] = "Puerto Rico",
        ["KP2"] = "US Virgin Islands", ["NP2"] = "US Virgin Islands",
        ["KH2"] = "Guam", ["KG4"] = "Guantanamo Bay",
        ["VE"] = "Canada", ["VA"] = "Canada", ["VO"] = "Canada", ["VY"] = "Canada", ["VE7"] = "Canada",
        ["XE"] = "Mexico", ["XF"] = "Mexico", ["4A"] = "Mexico", ["6D"] = "Mexico",
        ["CO"] = "Cuba", ["CM"] = "Cuba", ["HI"] = "Dominican Republic", ["HH"] = "Haiti",
        ["FM"] = "Martinique", ["FG"] = "Guadeloupe", ["J3"] = "Grenada", ["J6"] = "St. Lucia",
        ["TG"] = "Guatemala", ["YS"] = "El Salvador", ["HR"] = "Honduras", ["YN"] = "Nicaragua",
        ["TI"] = "Costa Rica", ["HP"] = "Panama", ["V3"] = "Belize",

        // South America
        ["PY"] = "Brazil", ["PP"] = "Brazil", ["PR"] = "Brazil", ["PT"] = "Brazil", ["PU"] = "Brazil", ["ZZ"] = "Brazil",
        ["LU"] = "Argentina", ["CE"] = "Chile", ["CX"] = "Uruguay", ["CP"] = "Bolivia",
        ["OA"] = "Peru", ["HC"] = "Ecuador", ["HK"] = "Colombia", ["YV"] = "Venezuela",
        ["ZP"] = "Paraguay", ["8R"] = "Guyana", ["PZ"] = "Suriname", ["FY"] = "French Guiana",

        // Europe
        ["G"] = "England", ["M"] = "England", ["2E"] = "England",
        ["GM"] = "Scotland", ["MM"] = "Scotland", ["2M"] = "Scotland",
        ["GW"] = "Wales", ["MW"] = "Wales", ["2W"] = "Wales",
        ["GI"] = "Northern Ireland", ["MI"] = "Northern Ireland",
        ["GD"] = "Isle of Man", ["GJ"] = "Jersey", ["GU"] = "Guernsey",
        ["EI"] = "Ireland", ["EJ"] = "Ireland",
        ["DL"] = "Germany", ["DA"] = "Germany", ["DB"] = "Germany", ["DC"] = "Germany",
        ["DD"] = "Germany", ["DF"] = "Germany", ["DG"] = "Germany", ["DH"] = "Germany",
        ["DJ"] = "Germany", ["DK"] = "Germany", ["DM"] = "Germany", ["DO"] = "Germany",
        ["F"] = "France", ["TM"] = "France",
        ["I"] = "Italy", ["IK"] = "Italy", ["IZ"] = "Italy", ["IW"] = "Italy",
        ["EA"] = "Spain", ["EB"] = "Spain", ["EC"] = "Spain", ["ED"] = "Spain",
        ["EA6"] = "Balearic Islands", ["EA8"] = "Canary Islands", ["EA9"] = "Ceuta & Melilla",
        ["CT"] = "Portugal", ["CT3"] = "Madeira", ["CU"] = "Azores",
        ["PA"] = "Netherlands", ["PB"] = "Netherlands", ["PD"] = "Netherlands", ["PE"] = "Netherlands", ["PI"] = "Netherlands",
        ["ON"] = "Belgium", ["OO"] = "Belgium", ["OT"] = "Belgium",
        ["LX"] = "Luxembourg", ["HB"] = "Switzerland", ["HB0"] = "Liechtenstein",
        ["OE"] = "Austria", ["OK"] = "Czech Republic", ["OL"] = "Czech Republic", ["OM"] = "Slovakia",
        ["SP"] = "Poland", ["HA"] = "Hungary", ["HG"] = "Hungary", ["YO"] = "Romania",
        ["LZ"] = "Bulgaria", ["YU"] = "Serbia", ["9A"] = "Croatia", ["S5"] = "Slovenia",
        ["Z3"] = "North Macedonia", ["E7"] = "Bosnia-Herzegovina", ["ZA"] = "Albania",
        ["SV"] = "Greece", ["SV5"] = "Dodecanese", ["SV9"] = "Crete", ["5B"] = "Cyprus",
        ["OH"] = "Finland", ["OH0"] = "Aland Islands", ["SM"] = "Sweden", ["SA"] = "Sweden",
        ["LA"] = "Norway", ["LB"] = "Norway", ["OZ"] = "Denmark", ["OY"] = "Faroe Islands", ["OX"] = "Greenland",
        ["TF"] = "Iceland", ["ES"] = "Estonia", ["YL"] = "Latvia", ["LY"] = "Lithuania",
        ["UA"] = "European Russia", ["RA"] = "European Russia", ["R"] = "European Russia",
        ["UR"] = "Ukraine", ["UT"] = "Ukraine", ["UY"] = "Ukraine", ["EW"] = "Belarus",
        ["ER"] = "Moldova", ["4L"] = "Georgia", ["EK"] = "Armenia", ["4J"] = "Azerbaijan",
        ["TA"] = "Turkey", ["TC"] = "Turkey",

        // Africa
        ["ZS"] = "South Africa", ["ZR"] = "South Africa", ["SU"] = "Egypt", ["CN"] = "Morocco",
        ["7X"] = "Algeria", ["3V"] = "Tunisia", ["5A"] = "Libya", ["5N"] = "Nigeria",
        ["5Z"] = "Kenya", ["5H"] = "Tanzania", ["9J"] = "Zambia", ["Z2"] = "Zimbabwe",
        ["V5"] = "Namibia", ["A2"] = "Botswana", ["3B8"] = "Mauritius", ["FR"] = "Reunion",
        ["EA8"] = "Canary Islands", ["D4"] = "Cape Verde", ["3C"] = "Equatorial Guinea",

        // Asia
        ["JA"] = "Japan", ["JE"] = "Japan", ["JF"] = "Japan", ["JG"] = "Japan", ["JH"] = "Japan",
        ["JI"] = "Japan", ["JJ"] = "Japan", ["JK"] = "Japan", ["JL"] = "Japan", ["JM"] = "Japan",
        ["JN"] = "Japan", ["JO"] = "Japan", ["JP"] = "Japan", ["JQ"] = "Japan", ["JR"] = "Japan", ["JS"] = "Japan",
        ["7J"] = "Japan", ["7K"] = "Japan", ["7L"] = "Japan", ["7M"] = "Japan", ["7N"] = "Japan", ["8J"] = "Japan",
        ["BY"] = "China", ["BG"] = "China", ["BA"] = "China", ["BD"] = "China", ["BH"] = "China",
        ["BV"] = "Taiwan", ["HL"] = "South Korea", ["DS"] = "South Korea", ["6K"] = "South Korea",
        ["HS"] = "Thailand", ["E2"] = "Thailand", ["9M"] = "Malaysia", ["9V"] = "Singapore",
        ["YB"] = "Indonesia", ["YC"] = "Indonesia", ["DU"] = "Philippines", ["DV"] = "Philippines",
        ["XV"] = "Vietnam", ["VU"] = "India", ["4S"] = "Sri Lanka", ["S2"] = "Bangladesh",
        ["AP"] = "Pakistan", ["EP"] = "Iran", ["YI"] = "Iraq", ["A4"] = "Oman", ["A6"] = "United Arab Emirates",
        ["A7"] = "Qatar", ["A9"] = "Bahrain", ["HZ"] = "Saudi Arabia", ["9K"] = "Kuwait",
        ["4X"] = "Israel", ["4Z"] = "Israel", ["JY"] = "Jordan", ["OD"] = "Lebanon", ["YK"] = "Syria",
        ["UN"] = "Kazakhstan", ["EX"] = "Kyrgyzstan", ["EY"] = "Tajikistan", ["EZ"] = "Turkmenistan",
        ["UK"] = "Uzbekistan", ["9N"] = "Nepal", ["XX9"] = "Macao", ["VR"] = "Hong Kong",

        // Oceania
        ["VK"] = "Australia", ["ZL"] = "New Zealand", ["KH8"] = "American Samoa", ["5W"] = "Samoa",
        ["ZK"] = "Cook Islands", ["3D2"] = "Fiji", ["FK"] = "New Caledonia", ["FO"] = "French Polynesia",
        ["YJ"] = "Vanuatu", ["P2"] = "Papua New Guinea", ["V7"] = "Marshall Islands", ["T8"] = "Palau",
        ["KH2"] = "Guam", ["V6"] = "Micronesia", ["T30"] = "Western Kiribati",
    };

    /// The DXCC entity for a callsign, or "Unknown" if the prefix isn't in the table.
    public static string Resolve(string? callsign)
    {
        if (string.IsNullOrWhiteSpace(callsign)) return "Unknown";
        string call = callsign.Trim().ToUpperInvariant();

        // Strip a portable suffix like "/P" or "/MM"; a prefix like "F/W1AW" means the
        // operator is in the prefixed entity — use the part before the slash.
        int slash = call.IndexOf('/');
        if (slash > 0)
        {
            string before = call[..slash];
            string after = call[(slash + 1)..];
            // Use whichever side looks like a country prefix (before, unless it's a
            // short qualifier like P/MM/QRP and the other side is a real call).
            call = (before.Length <= 3 && after.Length >= before.Length) ? before : before;
            if (before is "P" or "M" or "MM" or "AM" or "QRP" && after.Length > 0) call = after;
        }

        // Longest matching prefix wins (up to 4 leading characters).
        for (int len = Math.Min(4, call.Length); len >= 1; len--)
        {
            string p = call[..len];
            if (Prefixes.TryGetValue(p, out string? entity)) return entity;
        }
        return "Unknown";
    }
}
