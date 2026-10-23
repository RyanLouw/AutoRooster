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
    public static DemoSchool CreateRealistic(int seed,DateOnly firstDate)
    {
        var basic=Create(seed,firstDate);var random=new Random(seed^0x5a17);
        Guid Id(){var bytes=new byte[16];random.NextBytes(bytes);return new(bytes);}
        string[] firstNames=["Thandi","Sipho","Naledi","Pieter","Aisha","Lerato","John","Nomsa","Fatima","David","Zanele","Ryan","Mpho","Anne","Sizwe"];
        string[] lastNames=["Dlamini","Botha","Mokoena","Naidoo","Nkosi"];
        var teachers=basic.Request.Teachers.ToArray();
        for(int i=0;i<teachers.Length;i++)
        {
            teachers[i].FirstName=firstNames[i%firstNames.Length];teachers[i].LastName=lastNames[i/firstNames.Length];
            teachers[i].Notes=teachers[i].WorkloadWeight<1?"Department leadership / other responsibilities: reduced invigilation share.":"Demonstration teacher; adjust workload weight and availability to test redistribution.";
        }
        var subjects=SchoolSubjectCatalogue.Items.Select(s=>new Subject(s.Name,s.Code,Id())).ToArray();
        string[] juniorCodes=["ENG-HL","AFR-FAL","MATH","NS","SS","TECH","EMS","ARTS-J","LO"];
        var papers=new List<(Subject Subject,int Grade)>();
        foreach(var grade in new[]{8,9})foreach(var code in juniorCodes)papers.Add((subjects.Single(s=>s.SubjectCode==code),grade));
        int senior=0;
        foreach(var subject in subjects.Where(s=>!new[]{"NS","SS","TECH","EMS","ARTS-J"}.Contains(s.SubjectCode)))papers.Add((subject,10+senior++%3));
        var dates=new List<DateOnly>();for(var date=firstDate;dates.Count<(papers.Count+2)/3;date=date.AddDays(1))if(date.DayOfWeek is not(DayOfWeek.Saturday or DayOfWeek.Sunday))dates.Add(date);
        var exams=new List<Exam>();var allocations=new List<ExamVenue>();var known=new List<ScheduledAssignment>();
        for(int i=0;i<papers.Count;i++)
        {
            var slot=i%3;var start=slot switch{0=>new TimeOnly(8,0),1=>new TimeOnly(11,30),_=>new TimeOnly(14,0)};
            var duration=slot switch{0=>90+30*(i/3%4),1=>60+30*(i/3%3),_=>90+30*(i/3%3)};
            var exam=new Exam(papers[i].Subject.Id,papers[i].Grade,dates[i/3],start,start.AddMinutes(duration),Id()){Description=$"Realistic demo paper {i+1:00}"};exams.Add(exam);
            int duty=0;
            foreach(var venue in basic.Request.Venues.Take(exam.Grade<10?2:3))
            {
                var positions=exam.Grade>=10 && duty==0?2:1;
                var allocation=new ExamVenue(exam.Id,venue.Id,30+random.Next(10),positions,Id());allocations.Add(allocation);
                for(int position=0;position<positions;position++)known.Add(new(allocation.Id,teachers[30+(i*4+duty++)%45].Id,position,exam.StartTime,exam.EndTime));
            }
        }
        var settings=new SchedulingSettings{SchoolName="Realistic Demonstration School",AllowSessionSplitting=true,MinimumSessionMinutes=30,SchedulingTimeIncrementMinutes=30,SchedulingSeed=seed,SolverTimeLimitSeconds=30};
        var request=basic.Request with{Subjects=subjects,Exams=exams,ExamVenues=allocations,Configuration=settings};
        return new(request,known);
    }

}
