using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Web;

public class Todo
{
    public int Id { get; set; }

    [MaxLength(200)]
    public string Title { get; set; } = "";

    public bool IsDone { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TodoDbContext(DbContextOptions<TodoDbContext> options) : DbContext(options)
{
    public DbSet<Todo> Todos => Set<Todo>();
}

/// <summary>API のリクエスト/メッセージ用ペイロード。</summary>
public record CreateTodoRequest(string Title);

public record TodoCreatedMessage(int Id, string Title, DateTime CreatedAt);
