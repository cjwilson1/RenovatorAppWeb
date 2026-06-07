using RenovatorApp.Web.ViewModels;

namespace RenovatorApp.Web.Services;

public static class StateOptionsProvider
{
    private static readonly IReadOnlyList<StateOptionViewModel> States =
    [
        new("AL", "Alabama"),
        new("AK", "Alaska"),
        new("AS", "American Samoa"),
        new("AZ", "Arizona"),
        new("AR", "Arkansas"),
        new("CA", "California"),
        new("CO", "Colorado"),
        new("CT", "Connecticut"),
        new("DE", "Delaware"),
        new("DC", "District of Columbia"),
        new("FL", "Florida"),
        new("GA", "Georgia"),
        new("GU", "Guam"),
        new("HI", "Hawaii"),
        new("ID", "Idaho"),
        new("IL", "Illinois"),
        new("IN", "Indiana"),
        new("IA", "Iowa"),
        new("KS", "Kansas"),
        new("KY", "Kentucky"),
        new("LA", "Louisiana"),
        new("ME", "Maine"),
        new("MD", "Maryland"),
        new("MA", "Massachusetts"),
        new("MI", "Michigan"),
        new("MN", "Minnesota"),
        new("MS", "Mississippi"),
        new("MO", "Missouri"),
        new("MT", "Montana"),
        new("NE", "Nebraska"),
        new("NV", "Nevada"),
        new("NH", "New Hampshire"),
        new("NJ", "New Jersey"),
        new("NM", "New Mexico"),
        new("NY", "New York"),
        new("NC", "North Carolina"),
        new("ND", "North Dakota"),
        new("MP", "Northern Mariana Islands"),
        new("OH", "Ohio"),
        new("OK", "Oklahoma"),
        new("OR", "Oregon"),
        new("PA", "Pennsylvania"),
        new("PR", "Puerto Rico"),
        new("RI", "Rhode Island"),
        new("SC", "South Carolina"),
        new("SD", "South Dakota"),
        new("TN", "Tennessee"),
        new("TX", "Texas"),
        new("UM", "U.S. Minor Outlying Islands"),
        new("UT", "Utah"),
        new("VT", "Vermont"),
        new("VI", "U.S. Virgin Islands"),
        new("VA", "Virginia"),
        new("WA", "Washington"),
        new("WV", "West Virginia"),
        new("WI", "Wisconsin"),
        new("WY", "Wyoming")
    ];

    public static IReadOnlyList<StateOptionViewModel> GetStates() => States;
}
