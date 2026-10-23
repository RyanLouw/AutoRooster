using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class ThirtyMinuteHandoverTests
{
    [Theory]
    [InlineData(8,1)]
    [InlineData(9,1)]
    [InlineData(10,1)]
    [InlineData(11,1)]
    [InlineData(12,2)]
    public async Task EveryGradeCanHandOverAfterThirtyMinutesAndEachPositionStaysCovered(int grade,int positions)
    {
        var request=ValidationTests.Request(positions*2,positions);
        var exam=new Exam(request.Subjects[0].Id,grade,request.Exams[0].ExamDate,new(8,0),new(9,0));
        var allocation=new ExamVenue(exam.Id,request.Venues[0].Id,30,positions);
        request=request with {Exams=[exam],ExamVenues=[allocation],Availability=request.Teachers.Select((t,i)=>new TeacherAvailability(t.Id,exam.ExamDate,new(8,i%2==0?0:30),i%2==0?new(8,30):new(9,0),true)).ToArray()};
        var result=await new InvigilationSolver().SolveAsync(request);
        Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible,string.Join(";",result.Issues.Select(x=>x.Message)));
        Assert.Equal(positions*2,result.Assignments.Count);
        Assert.All(result.Assignments,a=>Assert.Equal(30,a.Interval.Minutes));
        Assert.Empty(RosterValidator.Validate(request,result.Assignments));
        for(int position=0;position<positions;position++)
        {
            var duties=result.Assignments.Where(a=>a.PositionIndex==position).OrderBy(a=>a.StartTime).ToArray();
            Assert.Equal(new TimeOnly(8,0),duties[0].StartTime);
            Assert.Equal(duties[0].EndTime,duties[1].StartTime);
            Assert.Equal(new TimeOnly(9,0),duties[1].EndTime);
            Assert.NotEqual(duties[0].TeacherId,duties[1].TeacherId);
        }
    }
    [Theory]
    [InlineData(8,1)]
    [InlineData(9,1)]
    [InlineData(10,1)]
    [InlineData(12,2)]
    public async Task FairnessCanChooseHalfHourHandoversEvenWhenAllTeachersAreAvailable(int grade,int positions)
    {
        var request=ValidationTests.Request(positions*2,positions);
        var exam=new Exam(request.Subjects[0].Id,grade,request.Exams[0].ExamDate,new(8,0),new(9,0));
        request=request with {Exams=[exam],ExamVenues=[new(exam.Id,request.Venues[0].Id,30,positions)]};
        var result=await new InvigilationSolver().SolveAsync(request);
        Assert.Equal(SchedulingStatus.Optimal,result.Status);Assert.Equal(positions*2,result.Assignments.Count);
        Assert.All(result.Assignments,a=>Assert.Equal(30,a.Interval.Minutes));Assert.Empty(RosterValidator.Validate(request,result.Assignments));
        Assert.Equal(0,result.Fairness.MaximumNormalizedDeviation);
    }

}
