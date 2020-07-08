﻿using Domain.Entities;
using Domain.ViewModels;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Persistence.Contexts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.SubscryptionConfigurationContext.Commands.Create
{
    public class CreateSubscryptionCommandValidator : AbstractValidator<CreateSubscryptionCommand>
    {
        private readonly MySqlContext _sqlContext;

        public CreateSubscryptionCommandValidator(MySqlContext sqlContext)
        {
            _sqlContext = sqlContext;

            RuleFor(c => c.TournamentId)
                .NotEmpty()
                    .WithMessage("Por favor, informe o torneio.")
                .Must((model, el) => IsTournamentAvailable(model.Mode, el).Result)
                    .WithMessage("Desculpe, as incrições para esse torneio acabam de encerrar.");

            RuleFor(c => c.Nickname)
                .NotEmpty()
                    .WithMessage("Por favor, informe o nickname ou nome da equipe.");

            RuleFor(c => c.Icon)
                .NotEmpty()
                    .WithMessage("Por favor, escolha um ícone.")
                .ExclusiveBetween(0, 19)
                    .WithMessage("Por favor, escolha um ícone válido.");

            RuleFor(c => c.Color)
                .NotEmpty()
                    .WithMessage("Por favor, escolha uma cor.")
                .ExclusiveBetween(0, 5)
                    .WithMessage("Por favor, escolha uma cor válida.");

            RuleFor(c => c.CondominiumID)
                .NotEmpty()
                    .When(c => c.Condominium == null)
                    .WithMessage("Por favor, selecione ou cadastre um condomínio.");

            RuleFor(c => c.Condominium)
                .NotEmpty()
                    .When(c => c.CondominiumID == null)
                    .WithMessage("Por favor, selecione ou cadastre um condomínio.");

            RuleFor(c => c.Mode)
                .IsInEnum()
                .NotEqual(Domain.Enums.eMode.Both)
                    .WithMessage("Por favor, selecione um modo de jogo.");

            RuleFor(c => c.Player)
                .NotEmpty()
                    .WithMessage("Por favor, preencha os dados do jogodor principal.")
                .Custom((el, validator) =>
                {
                    if (string.IsNullOrEmpty(el.CPF) || string.IsNullOrEmpty(el.Email) || 
                        string.IsNullOrEmpty(el.Name) || el.Document == null)
                        validator.AddFailure("Preencha todos os campos obrigatórios");
                })
                .Must((model, el) => IsPlayerInTournament(el, model.TournamentId).Result == null)
                    .WithMessage("Jogador já está inscrito no torneio.");

            RuleFor(c => c.Team)
                .NotEmpty()
                    .When(c => c.Mode == Domain.Enums.eMode.Team)
                    .WithMessage("Por favor, preencha os dados dos integrantes do time.")
                .Must((model, el) => _sqlContext.Set<Tournament>()
                                          .Where(t => t.Id == model.TournamentId)
                                          .FirstOrDefault()
                                          .PlayerLimit > el.Count)
                    .WithMessage("Número de integrantes ultrapassou o limite do campeonato.")
                .ForEach(item =>
                {
                    item
                        .Custom((el, validator) =>
                        {
                            if (string.IsNullOrEmpty(el.CPF) || string.IsNullOrEmpty(el.Email) ||
                                string.IsNullOrEmpty(el.Name) || el.Document == null)
                                validator.AddFailure("Preencha todos os campos obrigatórios");
                        })
                        .Must((model, el) => IsPlayerInTournament(el).Result == null)
                            .WithMessage("Jogador já está inscrito no torneio.");
                });
        }

        private Guid? tournamentID { get; set; }
        private async Task<bool> IsTournamentAvailable(Domain.Enums.eMode mode, Guid id)
        {
            var _tournament = await _sqlContext.Set<Tournament>()
                                          .Include(t => t.Teams)
                                          .Where(t => t.Id == id)
                                          .FirstOrDefaultAsync();

            var _avaliable = true;

            if (_tournament.StartSubscryption < DateTime.Now || _tournament.EndSubscryption > DateTime.Now)
                _avaliable = false;

            if (_avaliable && mode == Domain.Enums.eMode.Solo)
            {
                var _soloSub = _tournament.Teams.Where(s => s.Active && s.Mode == Domain.Enums.eMode.Solo).ToList().Count;
                if (_tournament.SubscryptionLimit > 0 && _soloSub == _tournament.SubscryptionLimit)
                    _avaliable = false;
            }

            if (_avaliable && mode == Domain.Enums.eMode.Team)
            {
                var _teamSub = _tournament.Teams.Where(s => s.Active && s.Mode == Domain.Enums.eMode.Team).ToList().Count;
                if (_tournament.SubscryptionLimit > 0 && _teamSub == _tournament.SubscryptionLimit)
                    _avaliable = false;
            }

            return _avaliable;
        }
        private async Task<Tournament> IsPlayerInTournament(TemplatePlayerVM player, Guid? id = null)
        {
            if (id.HasValue) tournamentID = id.Value;

            return await _sqlContext.Set<Tournament>()
                       .Include(t => t.Teams)
                           .ThenInclude(t => t.Players)
                               .ThenInclude(p => p.Player)
                       .Where(t => t.Id == tournamentID.Value &&
                                   t.Teams.Any(te => te.Active &&
                                                     te.Players.Any(p => p.Player.CPF.Equals(player.CPF))))
                       .FirstOrDefaultAsync();
        }
    }
}