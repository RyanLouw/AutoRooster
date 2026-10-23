using SchoolExamScheduler.Domain;
using SchoolExamScheduler.Scheduling;
using Xunit;

public sealed class FairnessTests
{
    internal static SchedulingRequest Period(int teachers=3,int days=5,int positions=1)
    {
        var r=SolverTests.Unsplit(teachers,positions);var exams=Enumerable.Range(0,days).Select(i=>new Exam(r.Subjects[0].Id,8,new DateOnly(2026,12,1).AddDays(i),new(8,0),new(10,0))).ToArray();
        return r with{Exams=exams,ExamVenues=exams.Select(e=>new ExamVenue(e.Id,r.Venues[0].Id,30,positions)).ToArray()};
    }
    [Fact]
    public void ReducedResponsibilityWeightAndLeaveRedistributeTheSameDemand()
    {
        var r=Period(3,2);var before=FairnessCalculator.Targets(r);
        r.Teachers[0].WorkloadWeight=0.5m;
        var lighter=FairnessCalculator.Targets(r);
        Assert.True(lighter[r.Teachers[0].Id]<before[r.Teachers[0].Id]);
        Assert.True(lighter[r.Teachers[1].Id]>before[r.Teachers[1].Id]);
        r=r with{Availability=[new(r.Teachers[0].Id,r.Exams[0].ExamDate,new(7,0),new(17,0),false)]};
        var absent=FairnessCalculator.Targets(r);
        Assert.True(absent[r.Teachers[0].Id]<lighter[r.Teachers[0].Id]);
        Assert.True(absent[r.Teachers[1].Id]>lighter[r.Teachers[1].Id]);
        Assert.Equal(240,absent.Values.Sum(),5);
    }
    [Fact]
    public void TargetsRespectCapacityCapsAndRedistributeExcessToOtherTeachers()
    {
        var r=Period(3,2);r.Teachers[0].WorkloadWeight=100;r.Teachers[0].MaximumPeriodMinutes=30;
        var targets=FairnessCalculator.Targets(r);
        Assert.Equal(30,targets[r.Teachers[0].Id],5);
        Assert.Equal(105,targets[r.Teachers[1].Id],5);Assert.Equal(240,targets.Values.Sum(),5);
    }
    [Fact]
    public async Task WeightedTargetsAndWholePeriodAllocationAreTwoTwoOne()
    {
        var r=Period();r.Teachers[2].WorkloadWeight=0.5m;
        var result=await new InvigilationSolver().SolveAsync(r);
        Assert.Equal(SchedulingStatus.Optimal,result.Status);
        var rows=result.Fairness.Teachers.ToDictionary(x=>x.TeacherId);
        Assert.Equal(240,rows[r.Teachers[0].Id].TargetMinutes,5);Assert.Equal(240,rows[r.Teachers[0].Id].ActualMinutes);
        Assert.Equal(240,rows[r.Teachers[1].Id].ActualMinutes);Assert.Equal(120,rows[r.Teachers[2].Id].ActualMinutes);
        Assert.Equal(600,result.Fairness.TotalMinutes);Assert.Equal(0,result.Fairness.MaximumNormalizedDeviation);
        Assert.Empty(RosterValidator.Validate(r,result.Assignments));
    }
    [Fact]
    public void CapacityCountsDemandUnionOnlyAndAppliesDailyAndPeriodLimits()
    {
        var r=Period(days:2);r.Teachers[0].MaximumDailyMinutes=60;r.Teachers[0].MaximumPeriodMinutes=90;
        var capacity=FairnessCalculator.Capacities(r);
        Assert.Equal(90,capacity[r.Teachers[0].Id]);Assert.Equal(240,capacity[r.Teachers[1].Id]);
        var venue=new Venue("B",40);var exam=new Exam(r.Subjects[0].Id,9,r.Exams[0].ExamDate,new(8,0),new(10,0));
        r=r with{Exams=[..r.Exams,exam],Venues=[..r.Venues,venue],ExamVenues=[..r.ExamVenues,new(exam.Id,venue.Id,30,1)]};
        Assert.Equal(240,FairnessCalculator.Capacities(r)[r.Teachers[1].Id]);
    }
    [Fact]
    public void ZeroAvailabilityAndEmptyPeriodsHaveSafeStatistics()
    {
        var r=Period(days:1);r=r with{Availability=[new(r.Teachers[0].Id,r.Exams[0].ExamDate,new(7,0),new(17,0),false)]};
        var stats=FairnessCalculator.Calculate(r,[]);var row=stats.Teachers.Single(x=>x.TeacherId==r.Teachers[0].Id);
        Assert.Equal(0,row.TargetMinutes);Assert.Null(row.PercentageOfTarget);
        Assert.Equal(0,FairnessCalculator.Calculate(r with{Exams=[],ExamVenues=[]},[]).MaximumNormalizedDeviation);
    }
    [Fact]
    public async Task ShuffleDoesNotChangeFairnessQuality()
    {
        var r=Period();var solver=new InvigilationSolver();var a=await solver.SolveAsync(r);var b=await solver.SolveAsync(r with{Teachers=r.Teachers.Reverse().ToArray()});
        Assert.Equal(a.Fairness.MaximumNormalizedDeviation,b.Fairness.MaximumNormalizedDeviation,5);
        Assert.Equal(a.Assignments,b.Assignments);
    }
    [Fact]
    public async Task SeededRandomizedRostersSatisfyIndependentInvariants()
    {
        for(int seed=0;seed<12;seed++)
        {
            var random=new Random(seed);var r=Period(4,3,random.Next(1,3));r.Configuration.SchedulingSeed=seed;
            r.Teachers[0].WorkloadWeight=0.5m;r.Teachers[1].MaximumPeriodMinutes=240;
            r=r with{Availability=[new(r.Teachers[2].Id,r.Exams[0].ExamDate,new(8,0),new(9,0),false)]};
            var result=await new InvigilationSolver().SolveAsync(r);
            Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible,$"Seed {seed}: {result.Status}");
            Assert.Empty(RosterValidator.Validate(r,result.Assignments));
        }
    }
    [Fact]
    public async Task LargeSchoolImprovesOverFeasibleConcentratedBaseline()
    {
        var r=Period(75,10,8);r.Configuration.SolverTimeLimitSeconds=15;
        var result=await new InvigilationSolver().SolveAsync(r);
        Assert.True(result.Status is SchedulingStatus.Optimal or SchedulingStatus.Feasible,string.Join(";",result.Issues.Select(x=>x.Message)));
        Assert.Empty(RosterValidator.Validate(r,result.Assignments));
        var baseline=r.ExamVenues.SelectMany(v=>Enumerable.Range(0,8).Select(p=>new ScheduledAssignment(v.Id,r.Teachers[p].Id,p,new(8,0),new(10,0)))).ToArray();
        Assert.Empty(RosterValidator.Validate(r,baseline));
        Assert.True(result.Fairness.MaximumNormalizedDeviation<FairnessCalculator.Calculate(r,baseline).MaximumNormalizedDeviation);
    }
}
