using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
namespace SchoolExamScheduler.Application;
public sealed record DemoSchool(SchedulingRequest Request,IReadOnlyList<ScheduledAssignment> KnownFeasibleAssignments)
{
    public IReadOnlyList<object> Records=>[..Request.Teachers,..Request.Subjects,..Request.Venues,..Request.Exams,..Request.ExamVenues,..Request.Availability,Request.Configuration];
}
public static class DemoDataFactory
{
    public static DemoSchool Create(int seed,DateOnly firstDate,bool infeasible=false)
    {
        var random=new Random(seed);Guid Id(){var bytes=new byte[16];random.NextBytes(bytes);return new(bytes);}
        string[] firstNames=["Thandi","Sipho","Naledi","Pieter","Aisha","Lerato","John","Nomsa","Fatima","David","Zanele","Ryan","Mpho","Anne","Sizwe"];
        string[] lastNames=["Dlamini","Botha","Mokoena","Naidoo","Nkosi","Jacobs","Louw","Khumalo","Pillay","Molefe"];
        var teachers=Enumerable.Range(0,75).Select(i=>new Teacher(firstNames[random.Next(firstNames.Length)],lastNames[random.Next(lastNames.Length)],i%5==0?0.5m:i%7==0?1.5m:1m,Id()){EmployeeNumber=$"DEMO-{i+1:000}",MaximumDailyMinutes=360,MaximumPeriodMinutes=1800,CanInvigilate=!infeasible||i<2}).ToArray();
        var subjects=new[]{("Mathematics","MATH"),("English","ENG"),("Physical Sciences","SCI"),("Geography","GEO"),("Information Technology","IT"),("Business Studies","BUS")}.Select(x=>new Subject(x.Item1,x.Item2,Id())).ToArray();
        var venues=Enumerable.Range(0,7).Select(i=>new Venue($"Room {i+1:00}",40+i%2*10,Id())).ToArray();
        var exams=new List<Exam>();var allocations=new List<ExamVenue>();var availability=new List<TeacherAvailability>();var known=new List<ScheduledAssignment>();
        var dates=new List<DateOnly>();for(var d=firstDate;dates.Count<10;d=d.AddDays(1))if(d.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday))dates.Add(d);
        for(int day=0;day<dates.Count;day++)
        {
            int duty=0;
            for(int session=0;session<3;session++)
            {
                var start=session switch{0=>new TimeOnly(8,0),1=>new TimeOnly(9,0),_=>new TimeOnly(12,0)};
                var end=session switch{0=>new TimeOnly(10,0),1=>new TimeOnly(11,0),_=>new TimeOnly(13,30)};
                var exam=new Exam(subjects[(day+session)%subjects.Length].Id,8+(day+session)%5,dates[day],start,end,Id()){Description=$"Demonstration paper {day+1}.{session+1}"};exams.Add(exam);
                foreach(var venue in session==1?venues.Skip(4).Take(3):venues.Take(4))
                {
                    var allocation=new ExamVenue(exam.Id,venue.Id,30+random.Next(0,10),1,Id());allocations.Add(allocation);
                    known.Add(new(allocation.Id,teachers[30+(day*11+duty++)%45].Id,0,start,end));
                }
            }
        }
        for(int i=0;i<15;i++)availability.Add(new(teachers[i].Id,dates[i%dates.Count],new(7,0),new(17,0),false,Id()){Reason="Demonstration leave"});
        for(int i=15;i<30;i++)availability.Add(new(teachers[i].Id,dates[i%dates.Count],new(8,0),new(9,0),false,Id()){Reason="Demonstration school duty"});
        var settings=new SchedulingSettings {SchoolName="Demonstration School",AllowSessionSplitting=false,SchedulingSeed=seed,SolverTimeLimitSeconds=30};
        return new(new(){Teachers=teachers,Subjects=subjects,Venues=venues,Exams=exams,ExamVenues=allocations,Availability=availability,Configuration=settings},infeasible?[]:known);
    }
}
