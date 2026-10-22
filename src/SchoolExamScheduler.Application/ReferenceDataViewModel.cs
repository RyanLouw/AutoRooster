using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Application;
public partial class ReferenceDataViewModel(ISchoolDataStore store,ILogger<ReferenceDataViewModel> logger) : WorkspaceViewModel(logger)
{
    public ObservableCollection<Subject> Subjects {get;}=[];
    public ObservableCollection<Venue> Venues {get;}=[];
    public ObservableCollection<VenueUnavailability> VenueRestrictions {get;}=[];
    public IEnumerable<VenueUnavailability> VisibleVenueRestrictions=>VenueRestrictions.Where(x=>x.VenueId==SelectedVenue?.Id);
    [ObservableProperty] private Subject? selectedSubject;
    [ObservableProperty] private Venue? selectedVenue;
    [ObservableProperty] private string subjectName="";
    [ObservableProperty] private string subjectCode="";
    [ObservableProperty] private bool subjectActive=true;
    [ObservableProperty] private string venueName="";
    [ObservableProperty] private int capacity=30;
    [ObservableProperty] private bool venueActive=true;
    [ObservableProperty] private string venueNotes="";
    [ObservableProperty] private DateTime restrictionDate=DateTime.Today;
    [ObservableProperty] private string restrictionStart="07:00";
    [ObservableProperty] private string restrictionEnd="17:00";
    [ObservableProperty] private VenueUnavailability? selectedRestriction;
    partial void OnSelectedSubjectChanged(Subject? value){if(value is null)return;SubjectName=value.Name;SubjectCode=value.SubjectCode;SubjectActive=value.IsActive;}
    partial void OnSelectedVenueChanged(Venue? value){if(value is null)return;VenueName=value.Name;Capacity=value.Capacity;VenueActive=value.IsActive;VenueNotes=value.Notes;OnPropertyChanged(nameof(VisibleVenueRestrictions));}
    [RelayCommand] private Task LoadAsync()=>RunAsync(async()=>
    {
        Subjects.Clear();foreach(var row in await store.ListAsync<Subject>())Subjects.Add(row);
        Venues.Clear();foreach(var row in await store.ListAsync<Venue>())Venues.Add(row);
        VenueRestrictions.Clear();foreach(var row in await store.ListAsync<VenueUnavailability>())VenueRestrictions.Add(row);OnPropertyChanged(nameof(VisibleVenueRestrictions));
    });
    [RelayCommand] private void NewSubject(){SelectedSubject=null;SubjectName="";SubjectCode="";SubjectActive=true;}
    [RelayCommand] private void NewVenue(){SelectedVenue=null;VenueName="";Capacity=30;VenueActive=true;VenueNotes="";}
    [RelayCommand] private Task SaveSubjectAsync()=>RunAsync(async()=>
    {
        var valid=new Subject(SubjectName,SubjectCode);
        if(Subjects.Any(x=>x.Id!=SelectedSubject?.Id && string.Equals(x.SubjectCode,valid.SubjectCode,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Subject code is already in use.");
        var subject=SelectedSubject??valid;subject.Name=valid.Name;subject.SubjectCode=valid.SubjectCode;subject.IsActive=SubjectActive;
        await store.SaveAsync(subject);if(!Subjects.Contains(subject))Subjects.Add(subject);else Subjects[Subjects.IndexOf(subject)]=subject;SelectedSubject=subject;OnPropertyChanged(nameof(Subjects));
    });
    [RelayCommand] private Task SaveVenueAsync()=>RunAsync(async()=>
    {
        var valid=new Venue(VenueName,Capacity);
        if(Venues.Any(x=>x.Id!=SelectedVenue?.Id && string.Equals(x.Name,valid.Name,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Venue name is already in use.");
        var venue=SelectedVenue??valid;venue.Name=valid.Name;venue.Capacity=valid.Capacity;venue.IsActive=VenueActive;venue.Notes=VenueNotes;
        await store.SaveAsync(venue);if(!Venues.Contains(venue))Venues.Add(venue);else Venues[Venues.IndexOf(venue)]=venue;SelectedVenue=venue;OnPropertyChanged(nameof(Venues));
    });
    [RelayCommand] private Task AddRestrictionAsync()=>RunAsync(async()=>
    {
        if(SelectedVenue is null)throw new ArgumentException("Select a venue first.");
        var record=new VenueUnavailability(SelectedVenue.Id,DateOnly.FromDateTime(RestrictionDate),FormTimes.Parse(RestrictionStart),FormTimes.Parse(RestrictionEnd));
        await store.SaveAsync(record);VenueRestrictions.Add(record);OnPropertyChanged(nameof(VisibleVenueRestrictions));
    });
    [RelayCommand] private Task RemoveRestrictionAsync()=>RunAsync(async()=>
    {
        if(SelectedRestriction is null)throw new ArgumentException("Select a venue restriction first.");
        await store.DeleteAsync(SelectedRestriction);VenueRestrictions.Remove(SelectedRestriction);SelectedRestriction=null;OnPropertyChanged(nameof(VisibleVenueRestrictions));
    });
}
public static class FormTimes
{
    public static TimeOnly Parse(string value)=>TimeOnly.TryParseExact(value,"HH:mm",System.Globalization.CultureInfo.InvariantCulture,System.Globalization.DateTimeStyles.None,out var result)?result:throw new ArgumentException("Use a 24-hour time such as 08:30.");
}
