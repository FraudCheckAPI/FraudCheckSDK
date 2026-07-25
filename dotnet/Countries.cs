namespace FraudCheck.Client;

/// <summary>
/// ISO 3166-1 alpha-2 country codes by English name, for the <c>shipping_country</c> input —
/// <c>Countries.UnitedStates</c> reads better than a bare "US" and can't be typo'd. String constants, not
/// an enum, by the same rule as <see cref="ReasonCodes"/>: constants can't break deserialization and the
/// API accepts any valid code either way. The API also accepts alpha-3 ("USA") and ISO numeric ("840")
/// forms and normalizes responses to alpha-2.
/// </summary>
public static class Countries
{
    /// <summary>Afghanistan.</summary>
    public const string Afghanistan = "AF";

    /// <summary>Åland Islands.</summary>
    public const string AlandIslands = "AX";

    /// <summary>Albania.</summary>
    public const string Albania = "AL";

    /// <summary>Algeria.</summary>
    public const string Algeria = "DZ";

    /// <summary>American Samoa.</summary>
    public const string AmericanSamoa = "AS";

    /// <summary>Andorra.</summary>
    public const string Andorra = "AD";

    /// <summary>Angola.</summary>
    public const string Angola = "AO";

    /// <summary>Anguilla.</summary>
    public const string Anguilla = "AI";

    /// <summary>Antarctica.</summary>
    public const string Antarctica = "AQ";

    /// <summary>Antigua and Barbuda.</summary>
    public const string AntiguaAndBarbuda = "AG";

    /// <summary>Argentina.</summary>
    public const string Argentina = "AR";

    /// <summary>Armenia.</summary>
    public const string Armenia = "AM";

    /// <summary>Aruba.</summary>
    public const string Aruba = "AW";

    /// <summary>Australia.</summary>
    public const string Australia = "AU";

    /// <summary>Austria.</summary>
    public const string Austria = "AT";

    /// <summary>Azerbaijan.</summary>
    public const string Azerbaijan = "AZ";

    /// <summary>Bahamas.</summary>
    public const string Bahamas = "BS";

    /// <summary>Bahrain.</summary>
    public const string Bahrain = "BH";

    /// <summary>Bangladesh.</summary>
    public const string Bangladesh = "BD";

    /// <summary>Barbados.</summary>
    public const string Barbados = "BB";

    /// <summary>Belarus.</summary>
    public const string Belarus = "BY";

    /// <summary>Belgium.</summary>
    public const string Belgium = "BE";

    /// <summary>Belize.</summary>
    public const string Belize = "BZ";

    /// <summary>Benin.</summary>
    public const string Benin = "BJ";

    /// <summary>Bermuda.</summary>
    public const string Bermuda = "BM";

    /// <summary>Bhutan.</summary>
    public const string Bhutan = "BT";

    /// <summary>Bolivia, Plurinational State of.</summary>
    public const string Bolivia = "BO";

    /// <summary>Bonaire, Sint Eustatius and Saba.</summary>
    public const string Bonaire = "BQ";

    /// <summary>Bosnia and Herzegovina.</summary>
    public const string BosniaAndHerzegovina = "BA";

    /// <summary>Botswana.</summary>
    public const string Botswana = "BW";

    /// <summary>Bouvet Island.</summary>
    public const string BouvetIsland = "BV";

    /// <summary>Brazil.</summary>
    public const string Brazil = "BR";

    /// <summary>British Indian Ocean Territory.</summary>
    public const string BritishIndianOceanTerritory = "IO";

    /// <summary>Virgin Islands (British).</summary>
    public const string BritishVirginIslands = "VG";

    /// <summary>Brunei Darussalam.</summary>
    public const string Brunei = "BN";

    /// <summary>Bulgaria.</summary>
    public const string Bulgaria = "BG";

    /// <summary>Burkina Faso.</summary>
    public const string BurkinaFaso = "BF";

    /// <summary>Burundi.</summary>
    public const string Burundi = "BI";

    /// <summary>Cabo Verde.</summary>
    public const string CaboVerde = "CV";

    /// <summary>Cambodia.</summary>
    public const string Cambodia = "KH";

    /// <summary>Cameroon.</summary>
    public const string Cameroon = "CM";

    /// <summary>Canada.</summary>
    public const string Canada = "CA";

    /// <summary>Cayman Islands.</summary>
    public const string CaymanIslands = "KY";

    /// <summary>Central African Republic.</summary>
    public const string CentralAfricanRepublic = "CF";

    /// <summary>Chad.</summary>
    public const string Chad = "TD";

    /// <summary>Chile.</summary>
    public const string Chile = "CL";

    /// <summary>China.</summary>
    public const string China = "CN";

    /// <summary>Christmas Island.</summary>
    public const string ChristmasIsland = "CX";

    /// <summary>Cocos (Keeling) Islands.</summary>
    public const string Cocos = "CC";

    /// <summary>Colombia.</summary>
    public const string Colombia = "CO";

    /// <summary>Comoros.</summary>
    public const string Comoros = "KM";

    /// <summary>Cook Islands.</summary>
    public const string CookIslands = "CK";

    /// <summary>Costa Rica.</summary>
    public const string CostaRica = "CR";

    /// <summary>Côte d'Ivoire.</summary>
    public const string CoteDIvoire = "CI";

    /// <summary>Croatia.</summary>
    public const string Croatia = "HR";

    /// <summary>Cuba.</summary>
    public const string Cuba = "CU";

    /// <summary>Curaçao.</summary>
    public const string Curacao = "CW";

    /// <summary>Cyprus.</summary>
    public const string Cyprus = "CY";

    /// <summary>Czechia.</summary>
    public const string Czechia = "CZ";

    /// <summary>Congo, Democratic Republic of the.</summary>
    public const string DemocraticRepublicOfTheCongo = "CD";

    /// <summary>Denmark.</summary>
    public const string Denmark = "DK";

    /// <summary>Djibouti.</summary>
    public const string Djibouti = "DJ";

    /// <summary>Dominica.</summary>
    public const string Dominica = "DM";

    /// <summary>Dominican Republic.</summary>
    public const string DominicanRepublic = "DO";

    /// <summary>Ecuador.</summary>
    public const string Ecuador = "EC";

    /// <summary>Egypt.</summary>
    public const string Egypt = "EG";

    /// <summary>El Salvador.</summary>
    public const string ElSalvador = "SV";

    /// <summary>Equatorial Guinea.</summary>
    public const string EquatorialGuinea = "GQ";

    /// <summary>Eritrea.</summary>
    public const string Eritrea = "ER";

    /// <summary>Estonia.</summary>
    public const string Estonia = "EE";

    /// <summary>Eswatini.</summary>
    public const string Eswatini = "SZ";

    /// <summary>Ethiopia.</summary>
    public const string Ethiopia = "ET";

    /// <summary>Falkland Islands (Malvinas).</summary>
    public const string FalklandIslands = "FK";

    /// <summary>Faroe Islands.</summary>
    public const string FaroeIslands = "FO";

    /// <summary>Fiji.</summary>
    public const string Fiji = "FJ";

    /// <summary>Finland.</summary>
    public const string Finland = "FI";

    /// <summary>France.</summary>
    public const string France = "FR";

    /// <summary>French Guiana.</summary>
    public const string FrenchGuiana = "GF";

    /// <summary>French Polynesia.</summary>
    public const string FrenchPolynesia = "PF";

    /// <summary>French Southern Territories.</summary>
    public const string FrenchSouthernTerritories = "TF";

    /// <summary>Gabon.</summary>
    public const string Gabon = "GA";

    /// <summary>Gambia.</summary>
    public const string Gambia = "GM";

    /// <summary>Georgia.</summary>
    public const string Georgia = "GE";

    /// <summary>Germany.</summary>
    public const string Germany = "DE";

    /// <summary>Ghana.</summary>
    public const string Ghana = "GH";

    /// <summary>Gibraltar.</summary>
    public const string Gibraltar = "GI";

    /// <summary>Greece.</summary>
    public const string Greece = "GR";

    /// <summary>Greenland.</summary>
    public const string Greenland = "GL";

    /// <summary>Grenada.</summary>
    public const string Grenada = "GD";

    /// <summary>Guadeloupe.</summary>
    public const string Guadeloupe = "GP";

    /// <summary>Guam.</summary>
    public const string Guam = "GU";

    /// <summary>Guatemala.</summary>
    public const string Guatemala = "GT";

    /// <summary>Guernsey.</summary>
    public const string Guernsey = "GG";

    /// <summary>Guinea.</summary>
    public const string Guinea = "GN";

    /// <summary>Guinea-Bissau.</summary>
    public const string GuineaBissau = "GW";

    /// <summary>Guyana.</summary>
    public const string Guyana = "GY";

    /// <summary>Haiti.</summary>
    public const string Haiti = "HT";

    /// <summary>Heard Island and McDonald Islands.</summary>
    public const string HeardIslandAndMcDonaldIslands = "HM";

    /// <summary>Honduras.</summary>
    public const string Honduras = "HN";

    /// <summary>Hong Kong.</summary>
    public const string HongKong = "HK";

    /// <summary>Hungary.</summary>
    public const string Hungary = "HU";

    /// <summary>Iceland.</summary>
    public const string Iceland = "IS";

    /// <summary>India.</summary>
    public const string India = "IN";

    /// <summary>Indonesia.</summary>
    public const string Indonesia = "ID";

    /// <summary>Iran, Islamic Republic of.</summary>
    public const string Iran = "IR";

    /// <summary>Iraq.</summary>
    public const string Iraq = "IQ";

    /// <summary>Ireland.</summary>
    public const string Ireland = "IE";

    /// <summary>Isle of Man.</summary>
    public const string IsleOfMan = "IM";

    /// <summary>Israel.</summary>
    public const string Israel = "IL";

    /// <summary>Italy.</summary>
    public const string Italy = "IT";

    /// <summary>Jamaica.</summary>
    public const string Jamaica = "JM";

    /// <summary>Japan.</summary>
    public const string Japan = "JP";

    /// <summary>Jersey.</summary>
    public const string Jersey = "JE";

    /// <summary>Jordan.</summary>
    public const string Jordan = "JO";

    /// <summary>Kazakhstan.</summary>
    public const string Kazakhstan = "KZ";

    /// <summary>Kenya.</summary>
    public const string Kenya = "KE";

    /// <summary>Netherlands, Kingdom of the.</summary>
    public const string KingdomOfTheNetherlands = "NL";

    /// <summary>Kiribati.</summary>
    public const string Kiribati = "KI";

    /// <summary>Kuwait.</summary>
    public const string Kuwait = "KW";

    /// <summary>Kyrgyzstan.</summary>
    public const string Kyrgyzstan = "KG";

    /// <summary>Lao People's Democratic Republic.</summary>
    public const string Laos = "LA";

    /// <summary>Latvia.</summary>
    public const string Latvia = "LV";

    /// <summary>Lebanon.</summary>
    public const string Lebanon = "LB";

    /// <summary>Lesotho.</summary>
    public const string Lesotho = "LS";

    /// <summary>Liberia.</summary>
    public const string Liberia = "LR";

    /// <summary>Libya.</summary>
    public const string Libya = "LY";

    /// <summary>Liechtenstein.</summary>
    public const string Liechtenstein = "LI";

    /// <summary>Lithuania.</summary>
    public const string Lithuania = "LT";

    /// <summary>Luxembourg.</summary>
    public const string Luxembourg = "LU";

    /// <summary>Macao.</summary>
    public const string Macao = "MO";

    /// <summary>Madagascar.</summary>
    public const string Madagascar = "MG";

    /// <summary>Malawi.</summary>
    public const string Malawi = "MW";

    /// <summary>Malaysia.</summary>
    public const string Malaysia = "MY";

    /// <summary>Maldives.</summary>
    public const string Maldives = "MV";

    /// <summary>Mali.</summary>
    public const string Mali = "ML";

    /// <summary>Malta.</summary>
    public const string Malta = "MT";

    /// <summary>Marshall Islands.</summary>
    public const string MarshallIslands = "MH";

    /// <summary>Martinique.</summary>
    public const string Martinique = "MQ";

    /// <summary>Mauritania.</summary>
    public const string Mauritania = "MR";

    /// <summary>Mauritius.</summary>
    public const string Mauritius = "MU";

    /// <summary>Mayotte.</summary>
    public const string Mayotte = "YT";

    /// <summary>Mexico.</summary>
    public const string Mexico = "MX";

    /// <summary>Micronesia, Federated States of.</summary>
    public const string Micronesia = "FM";

    /// <summary>Moldova, Republic of.</summary>
    public const string Moldova = "MD";

    /// <summary>Monaco.</summary>
    public const string Monaco = "MC";

    /// <summary>Mongolia.</summary>
    public const string Mongolia = "MN";

    /// <summary>Montenegro.</summary>
    public const string Montenegro = "ME";

    /// <summary>Montserrat.</summary>
    public const string Montserrat = "MS";

    /// <summary>Morocco.</summary>
    public const string Morocco = "MA";

    /// <summary>Mozambique.</summary>
    public const string Mozambique = "MZ";

    /// <summary>Myanmar.</summary>
    public const string Myanmar = "MM";

    /// <summary>Namibia.</summary>
    public const string Namibia = "NA";

    /// <summary>Nauru.</summary>
    public const string Nauru = "NR";

    /// <summary>Nepal.</summary>
    public const string Nepal = "NP";

    /// <summary>New Caledonia.</summary>
    public const string NewCaledonia = "NC";

    /// <summary>New Zealand.</summary>
    public const string NewZealand = "NZ";

    /// <summary>Nicaragua.</summary>
    public const string Nicaragua = "NI";

    /// <summary>Niger.</summary>
    public const string Niger = "NE";

    /// <summary>Nigeria.</summary>
    public const string Nigeria = "NG";

    /// <summary>Niue.</summary>
    public const string Niue = "NU";

    /// <summary>Norfolk Island.</summary>
    public const string NorfolkIsland = "NF";

    /// <summary>Korea, Democratic People's Republic of.</summary>
    public const string NorthKorea = "KP";

    /// <summary>North Macedonia.</summary>
    public const string NorthMacedonia = "MK";

    /// <summary>Northern Mariana Islands.</summary>
    public const string NorthernMarianaIslands = "MP";

    /// <summary>Norway.</summary>
    public const string Norway = "NO";

    /// <summary>Oman.</summary>
    public const string Oman = "OM";

    /// <summary>Pakistan.</summary>
    public const string Pakistan = "PK";

    /// <summary>Palau.</summary>
    public const string Palau = "PW";

    /// <summary>Palestine, State of.</summary>
    public const string Palestine = "PS";

    /// <summary>Panama.</summary>
    public const string Panama = "PA";

    /// <summary>Papua New Guinea.</summary>
    public const string PapuaNewGuinea = "PG";

    /// <summary>Paraguay.</summary>
    public const string Paraguay = "PY";

    /// <summary>Peru.</summary>
    public const string Peru = "PE";

    /// <summary>Philippines.</summary>
    public const string Philippines = "PH";

    /// <summary>Pitcairn.</summary>
    public const string Pitcairn = "PN";

    /// <summary>Poland.</summary>
    public const string Poland = "PL";

    /// <summary>Portugal.</summary>
    public const string Portugal = "PT";

    /// <summary>Puerto Rico.</summary>
    public const string PuertoRico = "PR";

    /// <summary>Qatar.</summary>
    public const string Qatar = "QA";

    /// <summary>Congo.</summary>
    public const string RepublicOfTheCongo = "CG";

    /// <summary>Réunion.</summary>
    public const string Reunion = "RE";

    /// <summary>Romania.</summary>
    public const string Romania = "RO";

    /// <summary>Russian Federation.</summary>
    public const string Russia = "RU";

    /// <summary>Rwanda.</summary>
    public const string Rwanda = "RW";

    /// <summary>Saint Barthélemy.</summary>
    public const string SaintBarthelemy = "BL";

    /// <summary>Saint Helena, Ascension and Tristan da Cunha.</summary>
    public const string SaintHelena = "SH";

    /// <summary>Saint Kitts and Nevis.</summary>
    public const string SaintKittsAndNevis = "KN";

    /// <summary>Saint Lucia.</summary>
    public const string SaintLucia = "LC";

    /// <summary>Saint Martin (French part).</summary>
    public const string SaintMartin = "MF";

    /// <summary>Saint Pierre and Miquelon.</summary>
    public const string SaintPierreAndMiquelon = "PM";

    /// <summary>Saint Vincent and the Grenadines.</summary>
    public const string SaintVincentAndTheGrenadines = "VC";

    /// <summary>Samoa.</summary>
    public const string Samoa = "WS";

    /// <summary>San Marino.</summary>
    public const string SanMarino = "SM";

    /// <summary>Sao Tome and Principe.</summary>
    public const string SaoTomeAndPrincipe = "ST";

    /// <summary>Saudi Arabia.</summary>
    public const string SaudiArabia = "SA";

    /// <summary>Senegal.</summary>
    public const string Senegal = "SN";

    /// <summary>Serbia.</summary>
    public const string Serbia = "RS";

    /// <summary>Seychelles.</summary>
    public const string Seychelles = "SC";

    /// <summary>Sierra Leone.</summary>
    public const string SierraLeone = "SL";

    /// <summary>Singapore.</summary>
    public const string Singapore = "SG";

    /// <summary>Sint Maarten (Dutch part).</summary>
    public const string SintMaarten = "SX";

    /// <summary>Slovakia.</summary>
    public const string Slovakia = "SK";

    /// <summary>Slovenia.</summary>
    public const string Slovenia = "SI";

    /// <summary>Solomon Islands.</summary>
    public const string SolomonIslands = "SB";

    /// <summary>Somalia.</summary>
    public const string Somalia = "SO";

    /// <summary>South Africa.</summary>
    public const string SouthAfrica = "ZA";

    /// <summary>South Georgia and the South Sandwich Islands.</summary>
    public const string SouthGeorgiaAndTheSouthSandwichIslands = "GS";

    /// <summary>Korea, Republic of.</summary>
    public const string SouthKorea = "KR";

    /// <summary>South Sudan.</summary>
    public const string SouthSudan = "SS";

    /// <summary>Spain.</summary>
    public const string Spain = "ES";

    /// <summary>Sri Lanka.</summary>
    public const string SriLanka = "LK";

    /// <summary>Sudan.</summary>
    public const string Sudan = "SD";

    /// <summary>Suriname.</summary>
    public const string Suriname = "SR";

    /// <summary>Svalbard and Jan Mayen.</summary>
    public const string SvalbardAndJanMayen = "SJ";

    /// <summary>Sweden.</summary>
    public const string Sweden = "SE";

    /// <summary>Switzerland.</summary>
    public const string Switzerland = "CH";

    /// <summary>Syrian Arab Republic.</summary>
    public const string Syria = "SY";

    /// <summary>Taiwan, Province of China.</summary>
    public const string Taiwan = "TW";

    /// <summary>Tajikistan.</summary>
    public const string Tajikistan = "TJ";

    /// <summary>Tanzania, United Republic of.</summary>
    public const string Tanzania = "TZ";

    /// <summary>Thailand.</summary>
    public const string Thailand = "TH";

    /// <summary>Timor-Leste.</summary>
    public const string TimorLeste = "TL";

    /// <summary>Togo.</summary>
    public const string Togo = "TG";

    /// <summary>Tokelau.</summary>
    public const string Tokelau = "TK";

    /// <summary>Tonga.</summary>
    public const string Tonga = "TO";

    /// <summary>Trinidad and Tobago.</summary>
    public const string TrinidadAndTobago = "TT";

    /// <summary>Tunisia.</summary>
    public const string Tunisia = "TN";

    /// <summary>Türkiye.</summary>
    public const string Turkiye = "TR";

    /// <summary>Turkmenistan.</summary>
    public const string Turkmenistan = "TM";

    /// <summary>Turks and Caicos Islands.</summary>
    public const string TurksAndCaicosIslands = "TC";

    /// <summary>Tuvalu.</summary>
    public const string Tuvalu = "TV";

    /// <summary>United States Minor Outlying Islands.</summary>
    public const string USMinorOutlyingIslands = "UM";

    /// <summary>Virgin Islands (U.S.).</summary>
    public const string USVirginIslands = "VI";

    /// <summary>Uganda.</summary>
    public const string Uganda = "UG";

    /// <summary>Ukraine.</summary>
    public const string Ukraine = "UA";

    /// <summary>United Arab Emirates.</summary>
    public const string UnitedArabEmirates = "AE";

    /// <summary>United Kingdom of Great Britain and Northern Ireland.</summary>
    public const string UnitedKingdom = "GB";

    /// <summary>United States of America.</summary>
    public const string UnitedStates = "US";

    /// <summary>Uruguay.</summary>
    public const string Uruguay = "UY";

    /// <summary>Uzbekistan.</summary>
    public const string Uzbekistan = "UZ";

    /// <summary>Vanuatu.</summary>
    public const string Vanuatu = "VU";

    /// <summary>Holy See.</summary>
    public const string VaticanCity = "VA";

    /// <summary>Venezuela, Bolivarian Republic of.</summary>
    public const string Venezuela = "VE";

    /// <summary>Viet Nam.</summary>
    public const string Vietnam = "VN";

    /// <summary>Wallis and Futuna.</summary>
    public const string WallisAndFutuna = "WF";

    /// <summary>Western Sahara.</summary>
    public const string WesternSahara = "EH";

    /// <summary>Yemen.</summary>
    public const string Yemen = "YE";

    /// <summary>Zambia.</summary>
    public const string Zambia = "ZM";

    /// <summary>Zimbabwe.</summary>
    public const string Zimbabwe = "ZW";
}
