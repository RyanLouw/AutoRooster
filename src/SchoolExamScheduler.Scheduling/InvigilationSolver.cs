using System.Globalization;
using Google.OrTools.Sat;
using SchoolExamScheduler.Domain;
namespace SchoolExamScheduler.Scheduling;

public sealed class InvigilationSolver
{
    private sealed record Candidate(ScheduledAssignment Assignment,Exam Exam,int Minutes,BoolVar Selected);
    public Task<SchedulingResult> SolveAsync(SchedulingRequest request,CancellationToken cancellationToken=default) =>
        Task.Run(()=>Solve(request,cancellationToken),CancellationToken.None);

    private static SchedulingResult Solve(SchedulingRequest request,CancellationToken cancellationToken)
    {
        SchedulingResult Result(SchedulingStatus status,IReadOnlyList<ScheduledAssignment>? assignments=null,IReadOnlyList<SchedulingIssue>? issues=null) =>
            new(status,assignments??[],issues??[],status is SchedulingStatus.Optimal or SchedulingStatus.Feasible ? FairnessCalculator.Calculate(request,assignments??[]) : new FairnessStatistics([]));
        if(cancellationToken.IsCancellationRequested)return Result(SchedulingStatus.Cancelled);
        var issues=RequestValidator.Validate(request);
        if(issues.Count>0)return Result(SchedulingStatus.InvalidInput,issues:issues);
        var model=new CpModel();var candidates=new List<Candidate>();
        var exams=request.Exams.ToDictionary(x=>x.Id);var settings=request.Configuration;
        var day=new TimeInterval(settings.DefaultDayStartTime,settings.DefaultDayEndTime);
        foreach(var allocation in request.ExamVenues.OrderBy(x=>x.Id))
        {
            if(cancellationToken.IsCancellationRequested)return Result(SchedulingStatus.Cancelled);
            var exam=exams[allocation.ExamId];if(!exam.IsActive)continue;
            for(int position=0;position<allocation.RequiredInvigilatorCount;position++)
            {
                var positionCandidates=new List<Candidate>();
                var boundaries=new SortedSet<TimeOnly>{exam.StartTime,exam.EndTime};
                if(settings.AllowSessionSplitting)
                {
                    for(var minute=exam.StartTime.ToTimeSpan().TotalMinutes+settings.SchedulingTimeIncrementMinutes;minute<exam.EndTime.ToTimeSpan().TotalMinutes;minute+=settings.SchedulingTimeIncrementMinutes)
                        boundaries.Add(TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minute)));
                    foreach(var locked in request.LockedAssignments.Where(x=>x.ExamVenueId==allocation.Id)) {boundaries.Add(locked.StartTime);boundaries.Add(locked.EndTime);}
                    foreach(var availability in request.Availability.Where(x=>x.Date==exam.ExamDate))
                    {
                        if(availability.StartTime>exam.StartTime && availability.StartTime<exam.EndTime)boundaries.Add(availability.StartTime);
                        if(availability.EndTime>exam.StartTime && availability.EndTime<exam.EndTime)boundaries.Add(availability.EndTime);
                    }
                }
                var times=boundaries.ToArray();
                foreach(var teacher in request.Teachers.Where(x=>x.IsEligible).OrderBy(x=>x.Id))
                for(int start=0;start<times.Length-1;start++)for(int end=start+1;end<times.Length;end++)
                {
                    var interval=new TimeInterval(times[start],times[end]);
                    if(interval!=exam.Interval && interval.Minutes<settings.MinimumSessionMinutes)continue;
                    if(!AvailabilityRules.IsAvailable(teacher.Id,exam.ExamDate,interval,day,request.Availability))continue;
                    var assignment=new ScheduledAssignment(allocation.Id,teacher.Id,position,interval.Start,interval.End);
                    var candidate=new Candidate(assignment,exam,interval.Minutes,model.NewBoolVar($"duty_{candidates.Count}"));
                    candidates.Add(candidate);positionCandidates.Add(candidate);
                }
                for(int boundary=0;boundary<times.Length-1;boundary++)
                    model.AddExactlyOne(positionCandidates.Where(x=>x.Assignment.StartTime<=times[boundary] && x.Assignment.EndTime>=times[boundary+1]).Select(x=>(ILiteral)x.Selected));
                model.Add(LinearExpr.Sum(positionCandidates.Select(x=>x.Selected))<=settings.MaximumSessionSegmentsPerPosition);
            }
        }
        foreach(var teacher in request.Teachers.Where(x=>x.IsEligible))
        {
            var duties=candidates.Where(x=>x.Assignment.TeacherId==teacher.Id).ToArray();
            foreach(var date in duties.GroupBy(x=>x.Exam.ExamDate))
            {
                // All candidate intervals have fixed times. A sweep of their start events
                // yields exact conflict cliques, avoiding thousands of optional interval objects.
                foreach(var start in date.Select(x=>x.Assignment.StartTime.ToTimeSpan().TotalMinutes).Distinct())
                {
                    var overlapping=date.Where(x=>x.Assignment.StartTime.ToTimeSpan().TotalMinutes<=start && x.Assignment.EndTime.ToTimeSpan().TotalMinutes+settings.MinimumBreakMinutes>start).Select(x=>(ILiteral)x.Selected).ToArray();
                    if(overlapping.Length>1)model.AddAtMostOne(overlapping);
                }
                model.Add(LinearExpr.WeightedSum(date.Select(x=>x.Selected),date.Select(x=>(long)x.Minutes))<= (teacher.MaximumDailyMinutes??settings.DefaultMaximumDailyMinutes));
            }
            if(teacher.MaximumPeriodMinutes is int limit) model.Add(LinearExpr.WeightedSum(duties.Select(x=>x.Selected),duties.Select(x=>(long)x.Minutes))<=limit);
        }
        foreach(var locked in request.LockedAssignments)
        {
            var matching=candidates.Where(x=>x.Assignment==locked).ToArray();
            if(matching.Length!=1)return Result(SchedulingStatus.Infeasible,issues:[new("LockedCoverage","The locked duty cannot be covered with current eligibility, availability and session settings.",locked.ExamVenueId,locked.TeacherId)]);
            model.Add(matching[0].Selected==1);
        }
        var solver=new CpSolver();using var registration=cancellationToken.Register(solver.StopSearch);
        var clock=System.Diagnostics.Stopwatch.StartNew();
        CpSolverStatus status=CpSolverStatus.Unknown;
        IReadOnlyList<ScheduledAssignment>? incumbent=null;
        bool allOptimal=true;
        // Solve pure feasibility before adding fairness auxiliaries. This provides an
        // independently checked incumbent even when later objective proofs take longer.
        solver.StringParameters=$"linearization_level:2 symmetry_level:0 cp_model_probing_level:0 num_search_workers:1 random_seed:{Math.Max(0,settings.SchedulingSeed)} stop_after_first_solution:true max_time_in_seconds:{Math.Max(0.05,settings.SolverTimeLimitSeconds*0.25).ToString(CultureInfo.InvariantCulture)}";
        try{status=solver.Solve(model);}
        catch(Exception e) when(e is not OutOfMemoryException){return Result(SchedulingStatus.SolverError,issues:[new("SolverError",e.Message)]);}
        if(status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
            incumbent=candidates.Where(x=>solver.BooleanValue(x.Selected)).Select(x=>x.Assignment).ToArray();
        if(status==CpSolverStatus.Infeasible)return Result(SchedulingStatus.Infeasible,issues:[new("Infeasible","No complete roster meets eligibility, coverage, availability, limits, breaks and locks. Review interacting constraints.")]);
        var targets=FairnessCalculator.Targets(request);
        var totalDemand=request.ExamVenues.Where(v=>exams[v.ExamId].IsActive).Sum(v=>(long)exams[v.ExamId].Interval.Minutes*v.RequiredInvigilatorCount);
        if(totalDemand>1_000_000)return Result(SchedulingStatus.InvalidInput,issues:[new("SizeLimit","Demand is too large for safe fixed-point objective arithmetic.")]);
        var deviations=new List<IntVar>();var unused=new List<BoolVar>();var dailyDeviations=new List<IntVar>();var counts=new List<IntVar>();
        var maxNormalized=model.NewIntVar(0,checked(totalDemand*1000+1000),"max_normalized_deviation");
        foreach(var teacher in request.Teachers.Where(x=>x.IsEligible).OrderBy(x=>x.Id))
        {
            var duties=candidates.Where(x=>x.Assignment.TeacherId==teacher.Id).ToArray();
            var minutes=LinearExpr.WeightedSum(duties.Select(x=>x.Selected),duties.Select(x=>(long)x.Minutes));
            var target=(long)Math.Round(targets[teacher.Id]*1000);
            var deviation=model.NewIntVar(0,checked(totalDemand*1000),$"deviation_{teacher.Id}");
            model.AddAbsEquality(deviation,minutes*1000-target);deviations.Add(deviation);
            if(target>0)model.Add(deviation*1000<=maxNormalized*Math.Max(1000,target));
            var count=model.NewIntVar(0,duties.Length,$"count_{teacher.Id}");
            model.Add(count==LinearExpr.Sum(duties.Select(x=>x.Selected)));counts.Add(count);
            if(target>0)
            {
                var zero=model.NewBoolVar($"unused_{teacher.Id}");model.Add(count==0).OnlyEnforceIf(zero);model.Add(count>=1).OnlyEnforceIf(zero.Not());unused.Add(zero);
            }
            // Secondary balance: keep teacher minutes spread across the available examination dates.
            var dates=duties.GroupBy(x=>x.Exam.ExamDate).ToArray();
            if(dates.Length>0)foreach(var date in dates)
            {
                var daily=LinearExpr.WeightedSum(date.Select(x=>x.Selected),date.Select(x=>(long)x.Minutes));
                var deviationDay=model.NewIntVar(0,checked(totalDemand*dates.Length),$"daily_deviation_{teacher.Id}_{date.Key}");
                model.AddAbsEquality(deviationDay,daily*dates.Length-minutes);dailyDeviations.Add(deviationDay);
            }
        }
        var maxDuties=model.NewIntVar(0,Math.Max(1,candidates.Count),"max_duties");
        if(counts.Count>0)model.AddMaxEquality(maxDuties,counts);
        var objectives=new List<LinearExpr>();
        if(settings.FairnessWeight>0){objectives.Add(maxNormalized);objectives.Add(LinearExpr.Sum(deviations));}
        if(settings.UnassignedTeacherPenalty>0)objectives.Add(LinearExpr.Sum(unused));
        objectives.Add(LinearExpr.Sum(dailyDeviations)*settings.DailyBalanceWeight+LinearExpr.Sum(candidates.Select(x=>x.Selected))+maxDuties*settings.DutyCountBalanceWeight);
        try
        {
            foreach(var objective in objectives)

            {
                if(cancellationToken.IsCancellationRequested){allOptimal=false;break;}
                var remaining=settings.SolverTimeLimitSeconds-clock.Elapsed.TotalSeconds;
                if(remaining<=0){allOptimal=false;break;}
                solver.StringParameters=$"linearization_level:2 symmetry_level:0 cp_model_probing_level:0 num_search_workers:1 random_seed:{Math.Max(0,settings.SchedulingSeed)} max_time_in_seconds:{remaining.ToString(CultureInfo.InvariantCulture)}";
                model.ClearHints();
                if(incumbent is not null)
                {
                    var selected=incumbent.ToHashSet();
                    foreach(var candidate in candidates)model.AddHint(candidate.Selected,selected.Contains(candidate.Assignment)?1:0);
                }
                model.Minimize(objective);status=solver.Solve(model);
                if(status is CpSolverStatus.Optimal or CpSolverStatus.Feasible)
                {
                    incumbent=candidates.Where(x=>solver.BooleanValue(x.Selected)).Select(x=>x.Assignment).ToArray();
                    if(status==CpSolverStatus.Optimal)model.Add(objective==solver.Value(objective));
                    else{allOptimal=false;break;}
                }
                else {allOptimal=false;break;}
            }
        }
        catch(Exception e) when(e is not OutOfMemoryException)
        {return Result(SchedulingStatus.SolverError,issues:[new("SolverError",e.Message)]);}
        if(incumbent is not null && !cancellationToken.IsCancellationRequested)
        {
            var validation=RosterValidator.Validate(request,incumbent);
            if(validation.Count>0)return Result(SchedulingStatus.SolverError,issues:validation);
            return Result(allOptimal?SchedulingStatus.Optimal:SchedulingStatus.Feasible,incumbent);
        }
        if(cancellationToken.IsCancellationRequested)return Result(SchedulingStatus.Cancelled);
        if(status is CpSolverStatus.Feasible or CpSolverStatus.Optimal)
        {
            var assignments=candidates.Where(x=>solver.BooleanValue(x.Selected)).Select(x=>x.Assignment).ToArray();
            var validation=RosterValidator.Validate(request,assignments);
            if(validation.Count>0)return Result(SchedulingStatus.SolverError,issues:validation);
            return Result(status==CpSolverStatus.Optimal?SchedulingStatus.Optimal:SchedulingStatus.Feasible,assignments);
        }
        return status switch
        {
            CpSolverStatus.Infeasible=>Result(SchedulingStatus.Infeasible,issues:[new("Infeasible","No complete roster meets all hard constraints. Review teacher shortages, availability, daily/period limits, breaks, locks and split settings; interacting constraints may be responsible.")]),
            CpSolverStatus.ModelInvalid=>Result(SchedulingStatus.SolverError,issues:[new("ModelInvalid",model.Validate())]),
            _=>Result(SchedulingStatus.TimeLimitWithoutSolution,issues:[new("TimeLimit","The solver stopped without finding a complete roster. This does not prove infeasibility.")])
        };
    }
}
