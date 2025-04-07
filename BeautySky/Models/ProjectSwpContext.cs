using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace BeautySky.Models;

public partial class ProjectSwpContext : DbContext
{
    public ProjectSwpContext()
    {
    }

    public ProjectSwpContext(DbContextOptions<ProjectSwpContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Answer> Answers { get; set; }

    public virtual DbSet<Blog> Blogs { get; set; }

    public virtual DbSet<CarePlan> CarePlans { get; set; }

    public virtual DbSet<CarePlanProducts> CarePlanProducts { get; set; }

    public virtual DbSet<CarePlanStep> CarePlanSteps { get; set; }

    public virtual DbSet<Cart> Carts { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Cart> Cart { get; set; }

    public virtual DbSet<News> News { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderProduct> OrderProducts { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<PaymentStatus> PaymentStatuses { get; set; }

    public virtual DbSet<PaymentType> PaymentTypes { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<ProductsImage> ProductsImages { get; set; }

    public virtual DbSet<Promotion> Promotions { get; set; }

    public virtual DbSet<Question> Questions { get; set; }

    public virtual DbSet<Quiz> Quizzes { get; set; }

    public virtual DbSet<Review> Reviews { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<SkinType> SkinTypes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserAnswer> UserAnswers { get; set; }

    public virtual DbSet<UserCarePlan> UserCarePlans { get; set; }

    public virtual DbSet<UserQuiz> UserQuizzes { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Answer>(entity =>
        {
            entity.HasKey(e => e.AnswerId).HasName("PK__Answer__D4825024A4A60BA2");

            entity.ToTable("Answer");

            entity.Property(e => e.AnswerId).HasColumnName("AnswerID");
            entity.Property(e => e.AnswerText).HasMaxLength(255);
            entity.Property(e => e.Point).HasMaxLength(255);
            entity.Property(e => e.QuestionId).HasColumnName("QuestionID");
            entity.Property(e => e.SkinTypeId)
                .HasMaxLength(255)
                .HasColumnName("SkinTypeID");

            entity.HasOne(d => d.Question).WithMany(p => p.Answers)
                .HasForeignKey(d => d.QuestionId)
                .HasConstraintName("FK__Answer__Question__6E01572D");
        });

        modelBuilder.Entity<Blog>(entity =>
        {
            entity.HasKey(e => e.BlogId).HasName("PK__Blog__54379E507747EC8C");

            entity.ToTable("Blog");

            entity.Property(e => e.BlogId).HasColumnName("BlogID");
            entity.Property(e => e.AuthorId).HasColumnName("AuthorID");
            entity.Property(e => e.Category).HasMaxLength(100);
            entity.Property(e => e.CreatedDate).HasColumnType("datetime");
            entity.Property(e => e.ImgUrl)
                .HasMaxLength(255)
                .HasColumnName("ImgURL");
            entity.Property(e => e.SkinType).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.UpdatedDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<CarePlan>(entity =>
        {
            entity.HasKey(e => e.CarePlanId).HasName("PK__CarePlan__2EB4A29DBC066487");

            entity.ToTable("CarePlan");

            entity.Property(e => e.CarePlanId).HasColumnName("CarePlanID");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.PlanName).HasMaxLength(255);
            entity.Property(e => e.SkinTypeId).HasColumnName("SkinTypeID");

            entity.HasOne(d => d.SkinType).WithMany(p => p.CarePlans)
                .HasForeignKey(d => d.SkinTypeId)
                .HasConstraintName("FK__CarePlan__SkinTy__6FE99F9F");
        });

        modelBuilder.Entity<CarePlanProducts>(entity =>
        {
            entity.HasKey(e => e.CarePlanProductId);

            entity.ToTable("CarePlanProduct");

            entity.Property(e => e.CarePlanId).HasColumnName("CarePlanID");
            entity.Property(e => e.StepId).HasColumnName("StepID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.CarePlan).WithMany(p => p.CarePlanProducts)
                .HasForeignKey(d => d.CarePlanId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CarePlanP__CareP__70DDC3D8");

            entity.HasOne(d => d.Step).WithMany(p => p.CarePlanProducts)
                .HasForeignKey(d => d.StepId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__CarePlanP__StepI__72C60C4A");

            entity.HasOne(d => d.User).WithMany(p => p.CarePlanProducts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__CarePlanP__UserI__2739D489");
        });

        modelBuilder.Entity<CarePlanStep>(entity =>
        {
            entity.HasKey(e => e.StepId).HasName("PK__CarePlan__2434333779ECD46E");

            entity.ToTable("CarePlanStep");

            entity.Property(e => e.StepId).HasColumnName("StepID");
            entity.Property(e => e.CarePlanId).HasColumnName("CarePlanID");
            entity.Property(e => e.StepDescription).HasMaxLength(255);
            entity.Property(e => e.StepName).HasMaxLength(255);

            entity.HasOne(d => d.CarePlan).WithMany(p => p.CarePlanSteps)
                .HasForeignKey(d => d.CarePlanId)
                .HasConstraintName("FK__CarePlanS__CareP__73BA3083");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B7B8E317F0");

            entity.ToTable("Cart");

            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.Carts)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__Cart__ProductId__503BEA1C");

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Cart__UserId__4F47C5E3");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasKey(e => e.CartId).HasName("PK__Cart__51BCD7B761D93432");

            entity.ToTable("Cart");

            entity.Property(e => e.TotalPrice).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Product).WithMany(p => p.Carts)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__Cart__ProductId__2A164134");

            entity.HasOne(d => d.User).WithMany(p => p.Carts)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Cart__UserId__29221CFB");
        });


        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId).HasName("PK__Category__19093A2BD360D2CE");

            entity.ToTable("Category");

            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.CategoryName).HasMaxLength(255);
        });

        modelBuilder.Entity<News>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__News__3214EC2780E3427B");

            entity.Property(e => e.Id).HasColumnName("ID");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .HasColumnName("ImageURL");
            entity.Property(e => e.StartDate).HasColumnType("datetime");
            entity.Property(e => e.Title).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Orders__C3905BAFCAD56814");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.FinalAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.OrderDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
            entity.Property(e => e.Status).HasMaxLength(255);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.CancelledReason).HasMaxLength(255);
            entity.Property(e => e.CancelledDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DeliveryDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.ShippingDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Payment).WithMany(p => p.Orders)
                .HasForeignKey(d => d.PaymentId)
                .HasConstraintName("FK__Orders__PaymentI__76969D2E");

            entity.HasOne(d => d.Promotion).WithMany(p => p.Orders)
                .HasForeignKey(d => d.PromotionId)
                .HasConstraintName("FK__Orders__Promotio__778AC167");

            entity.HasOne(d => d.User).WithMany(p => p.Orders)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Orders__UserID__787EE5A0");
        });

        modelBuilder.Entity<OrderProduct>(entity =>
        {
            entity.HasKey(e => new { e.OrderId, e.ProductId }).HasName("PK__OrderPro__08D097C166618DF2");

            entity.ToTable("OrderProduct");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.TotalPrice).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.UnitPrice).HasColumnType("decimal(10, 2)");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderProducts)
                .HasForeignKey(d => d.OrderId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderProd__Order__74AE54BC");

            entity.HasOne(d => d.Product).WithMany(p => p.OrderProducts)
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__OrderProd__Produ__75A278F5");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.HasKey(e => e.PaymentId).HasName("PK__Payment__9B556A58E69C4A52");

            entity.ToTable("Payment");

            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.PaymentDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PaymentStatusId).HasColumnName("PaymentStatusID");
            entity.Property(e => e.PaymentTypeId).HasColumnName("PaymentTypeID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.PaymentStatus).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaymentStatusId)
                .HasConstraintName("FK__Payment__Payment__7A672E12");

            entity.HasOne(d => d.PaymentType).WithMany(p => p.Payments)
                .HasForeignKey(d => d.PaymentTypeId)
                .HasConstraintName("FK__Payment__Payment__797309D9");

            entity.HasOne(d => d.User).WithMany(p => p.Payments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Payment__UserID__7B5B524B");
        });

        modelBuilder.Entity<PaymentStatus>(entity =>
        {
            entity.HasKey(e => e.PaymentStatusId).HasName("PK__PaymentS__34F8AC1FA7E39706");

            entity.ToTable("PaymentStatus");

            entity.Property(e => e.PaymentStatusId).HasColumnName("PaymentStatusID");
            entity.Property(e => e.PaymentStatus1)
                .HasMaxLength(255)
                .HasColumnName("PaymentStatus");
        });

        modelBuilder.Entity<PaymentType>(entity =>
        {
            entity.HasKey(e => e.PaymentTypeId).HasName("PK__PaymentT__BA430B15C07D0ACE");

            entity.ToTable("PaymentType");

            entity.Property(e => e.PaymentTypeId).HasColumnName("PaymentTypeID");
            entity.Property(e => e.PaymentTypeName).HasMaxLength(255);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.ProductId).HasName("PK__Products__B40CC6ED02C2A513");

            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.CategoryId).HasColumnName("CategoryID");
            entity.Property(e => e.Price).HasColumnType("decimal(10, 2)");
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.SkinTypeId).HasColumnName("SkinTypeID");
            entity.Property(e => e.Expire).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Category).WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .HasConstraintName("FK__Products__Catego__7C4F7684");

            entity.HasOne(d => d.SkinType).WithMany(p => p.Products)
                .HasForeignKey(d => d.SkinTypeId)
                .HasConstraintName("FK__Products__SkinTy__7D439ABD");
        });

        modelBuilder.Entity<ProductsImage>(entity =>
        {
            entity.HasKey(e => e.ProductsImageId).HasName("PK__Products__D92CC439BBEBA9BD");

            entity.ToTable("ProductsImage");

            entity.Property(e => e.ProductsImageId).HasColumnName("ProductsImageID");
            entity.Property(e => e.ImageDescription).HasMaxLength(255);
            entity.Property(e => e.ImageUrl)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");

            entity.HasOne(d => d.Product).WithMany(p => p.ProductsImages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__ProductsI__Produ__7E37BEF6");
        });

        modelBuilder.Entity<Promotion>(entity =>
        {
            entity.HasKey(e => e.PromotionId).HasName("PK__Promotio__52C42F2FB3E29679");

            entity.Property(e => e.PromotionId).HasColumnName("PromotionID");
            entity.Property(e => e.DiscountPercentage).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.EndDate).HasColumnType("datetime");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.PromotionName).HasMaxLength(255);
            entity.Property(e => e.StartDate).HasColumnType("datetime");
        });

        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.QuestionId).HasName("PK__Question__0DC06F8C8AFE052C");

            entity.ToTable("Question");

            entity.Property(e => e.QuestionId).HasColumnName("QuestionID");
            entity.Property(e => e.QuestionText).HasMaxLength(255);
            entity.Property(e => e.QuizId).HasColumnName("QuizID");

            entity.HasOne(d => d.Quiz).WithMany(p => p.Questions)
                .HasForeignKey(d => d.QuizId)
                .HasConstraintName("FK__Question__QuizID__7F2BE32F");
        });

        modelBuilder.Entity<Quiz>(entity =>
        {
            entity.HasKey(e => e.QuizId).HasName("PK__Quiz__8B42AE6E084D4957");

            entity.ToTable("Quiz");

            entity.Property(e => e.QuizId).HasColumnName("QuizID");
            entity.Property(e => e.DateCreated)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.QuizName).HasMaxLength(255);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.HasKey(e => e.ReviewId).HasName("PK__Review__74BC79AE2E0F249D");

            entity.ToTable("Review");

            entity.Property(e => e.ReviewId).HasColumnName("ReviewID");
            entity.Property(e => e.Comment).HasMaxLength(255);
            entity.Property(e => e.ProductId).HasColumnName("ProductID");
            entity.Property(e => e.ReviewDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.IsActive).HasDefaultValue(true);

            entity.HasOne(d => d.Product).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("FK__Review__ProductI__00200768");

            entity.HasOne(d => d.User).WithMany(p => p.Reviews)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__Review__UserID__01142BA1");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE3A83299CD2");

            entity.ToTable("Role");

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.RoleName)
                .HasMaxLength(255)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SkinType>(entity =>
        {
            entity.HasKey(e => e.SkinTypeId).HasName("PK__SkinType__D5D2962BCCBB16C7");

            entity.ToTable("SkinType");

            entity.Property(e => e.SkinTypeId).HasColumnName("SkinTypeID");
            entity.Property(e => e.SkinTypeName)
                .HasMaxLength(255)
                .HasColumnName("SkinType");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__Users__1788CCACF31B0E59");

            entity.HasIndex(e => e.Email, "UQ__Users__A9D105344097AC75").IsUnique();

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.ConfirmPassword)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.DateCreate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(255);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Phone)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.UserName)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.Point).HasColumnName("Point");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Users__RoleID__07C12930");
        });

        modelBuilder.Entity<UserAnswer>(entity =>
        {
            entity.HasKey(e => e.UserAnswerId).HasName("PK__UserAnsw__47CE235F99BD646D");

            entity.ToTable("UserAnswer");

            entity.Property(e => e.UserAnswerId).HasColumnName("UserAnswerID");
            entity.Property(e => e.AnswerId)
                .HasMaxLength(255)
                .HasColumnName("AnswerID");
            entity.Property(e => e.QuestionId)
                .HasMaxLength(255)
                .HasColumnName("QuestionID");
            entity.Property(e => e.SkinTypeId).HasColumnName("SkinTypeID");
            entity.Property(e => e.UserQuizId).HasColumnName("UserQuizID");

            entity.HasOne(d => d.SkinType).WithMany(p => p.UserAnswers)
                .HasForeignKey(d => d.SkinTypeId)
                .HasConstraintName("FK_UserAnswer_SkinType");

            entity.HasOne(d => d.UserQuiz).WithMany(p => p.UserAnswers)
                .HasForeignKey(d => d.UserQuizId)
                .HasConstraintName("FK__UserAnswe__UserQ__02084FDA");
        });

        modelBuilder.Entity<UserCarePlan>(entity =>
        {
            entity.HasKey(e => e.UserCarePlanId).HasName("PK__UserCare__9E6AF0B62C9A9A71");

            entity.ToTable("UserCarePlan");

            entity.Property(e => e.UserCarePlanId).HasColumnName("UserCarePlanID");
            entity.Property(e => e.CarePlanId).HasColumnName("CarePlanID");
            entity.Property(e => e.DateCreate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.CarePlan).WithMany(p => p.UserCarePlans)
                .HasForeignKey(d => d.CarePlanId)
                .HasConstraintName("FK__UserCareP__CareP__03F0984C");

            entity.HasOne(d => d.User).WithMany(p => p.UserCarePlans)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__UserCareP__UserI__04E4BC85");
        });

        modelBuilder.Entity<UserQuiz>(entity =>
        {
            entity.HasKey(e => e.UserQuizId).HasName("PK__UserQuiz__20FA63A782FD61B0");

            entity.ToTable("UserQuiz");

            entity.Property(e => e.UserQuizId).HasColumnName("UserQuizID");
            entity.Property(e => e.DateTaken)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.QuizId).HasColumnName("QuizID");
            entity.Property(e => e.UserId).HasColumnName("UserID");

            entity.HasOne(d => d.Quiz).WithMany(p => p.UserQuizzes)
                .HasForeignKey(d => d.QuizId)
                .HasConstraintName("FK__UserQuiz__QuizID__05D8E0BE");

            entity.HasOne(d => d.User).WithMany(p => p.UserQuizzes)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("FK__UserQuiz__UserID__06CD04F7");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
