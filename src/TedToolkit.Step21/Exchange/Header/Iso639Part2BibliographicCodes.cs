namespace TedToolkit.Step21;

/// <summary>Recognizes the ISO 639-2 Alpha-3 bibliographic code set referenced by ISO 10303-21.</summary>
internal static class Iso639Part2BibliographicCodes
{
    // Sorted fixed-width codes from the ISO 639-2 Registration Authority. Keeping one shared string avoids a
    // process-wide hash table and one managed string object per code.
    private const string Codes =
        "aarabkaceachadaadyafaafhafrainakaakkalbalealgaltamhanganpapaaraarcargarm" +
        "arnarpartarwasmastathausavaaveawaaymazebadbaibakbalbambanbaqbasbatbejbel" +
        "bembenberbhobihbikbinbisblabntbosbrabrebtkbuabugbulburbyncadcaicarcatcau" +
        "cebcelchachbchechgchichkchmchnchochpchrchuchvchycmccnrcopcorcoscpecpfcpp" +
        "crecrhcrpcsbcusczedakdandardaydeldendgrdindivdoidradsbduadumdutdyudzoefi" +
        "egyekaelxengenmepoesteweewofanfaofatfijfilfinfiufonfrefrmfrofrrfrsfryful" +
        "furgaagaygbagemgeogergezgilglagleglgglvgmhgohgongorgotgrbgrcgregrngswguj" +
        "gwihaihathauhawhebherhilhimhinhithmnhmohrvhsbhunhupibaiboiceidoiiiijoiku" +
        "ileiloinaincindineinhipkirairoitajavjbojpnjprjrbkaakabkackalkamkankarkas" +
        "kaukawkazkbdkhakhikhmkhokikkinkirkmbkokkomkonkorkoskpekrckrlkrokrukuakum" +
        "kurkutladlahlamlaolatlavlezlimlinlitlollozltzlualublugluilunluolusmacmad" +
        "magmahmaimakmalmanmaomapmarmasmaymdfmdrmenmgamicminmismkhmlgmltmncmnimno" +
        "mohmonmosmulmunmusmwlmwrmynmyvnahnainapnaunavnblndendondsnepnewnianicniu" +
        "nnonobnognonnornqonsonubnwcnyanymnynnyonziociojioriormosaossotaotopaapag" +
        "palpampanpappaupeoperphiphnplipolponporprapropusquerajraprarroarohromrum" +
        "runruprussadsagsahsaisalsamsansassatscnscoselsemsgasgnshnsidsinsiositsla" +
        "sloslvsmasmesmismjsmnsmosmssnasndsnksogsomsonsotspasrdsrnsrpsrrssasswsuk" +
        "sunsussuxswaswesycsyrtahtaitamtatteltemtertettgktglthatibtigtirtivtkltlh" +
        "tlitmhtogtontpitsitsntsotuktumtupturtuttvltwityvudmugauigukrumbundurduzb" +
        "vaivenvievolvotwakwalwarwaswelwenwlnwolxalxhoyaoyapyidyorypkzapzblzenzgh" +
        "zhazndzulzunzxxzza";

    internal static bool Contains(string value)
    {
        if (value.Length != 3)
            return false;
        if (string.CompareOrdinal(value, "qaa") >= 0 && string.CompareOrdinal(value, "qtz") <= 0)
            return true;

        var low = 0;
        var high = (Codes.Length / 3) - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var comparison = value.AsSpan().SequenceCompareTo(Codes.AsSpan(middle * 3, 3));
            if (comparison == 0)
                return true;
            if (comparison < 0)
                high = middle - 1;
            else
                low = middle + 1;
        }

        return false;
    }
}
