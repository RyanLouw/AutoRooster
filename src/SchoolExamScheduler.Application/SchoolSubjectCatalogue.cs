namespace SchoolExamScheduler.Application;

public static class SchoolSubjectCatalogue
{
    // A configurable South African secondary-school starting list, not a prescribed curriculum.
    public static IReadOnlyList<(string Name,string Code)> Items {get;}=
    [
        ("English Home Language","ENG-HL"),("English First Additional Language","ENG-FAL"),
        ("Afrikaans Home Language","AFR-HL"),("Afrikaans First Additional Language","AFR-FAL"),
        ("isiZulu","ZUL"),("isiXhosa","XHO"),("isiNdebele","NDE"),("Siswati","SSW"),
        ("Sesotho","SOT"),("Sepedi","NSO"),("Setswana","TSN"),("Tshivenda","VEN"),("Xitsonga","TSO"),
        ("South African Sign Language","SASL"),("Mathematics","MATH"),("Mathematical Literacy","MATH-LIT"),
        ("Technical Mathematics","TECH-MATH"),("Natural Sciences","NS"),("Social Sciences","SS"),
        ("Technology","TECH"),("Economic and Management Sciences","EMS"),("Creative Arts","ARTS-J"),
        ("Life Orientation","LO"),("Physical Sciences","SCI"),("Life Sciences","LIFE-SCI"),
        ("Technical Sciences","TECH-SCI"),("Geography","GEO"),("History","HIST"),("Accounting","ACC"),
        ("Business Studies","BUS"),("Economics","ECON"),("Computer Applications Technology","CAT"),
        ("Information Technology","IT"),("Agricultural Sciences","AGRI-SCI"),("Agricultural Technology","AGRI-TECH"),
        ("Agricultural Management Practices","AGRI-MGT"),("Civil Technology","CIVIL"),
        ("Electrical Technology","ELEC"),("Mechanical Technology","MECH"),("Engineering Graphics and Design","EGD"),
        ("Consumer Studies","CONSUMER"),("Hospitality Studies","HOSP"),("Tourism","TOUR"),
        ("Visual Arts","VIS-ART"),("Design","DESIGN"),("Dramatic Arts","DRAMA"),("Music","MUSIC"),("Dance Studies","DANCE"),
        ("Religion Studies","RELIGION")
    ];
}
